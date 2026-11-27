using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Spine;
using UnityEngine;

namespace CapybaraRebuild
{
    internal sealed class SpinePoseSource
    {
        private static readonly Dictionary<string,(SkeletonData data,Vector2 size)> DataCache=
            new Dictionary<string,(SkeletonData,Vector2)>(StringComparer.Ordinal);
        private readonly Dictionary<string,Spine.Animation> clips;
        private readonly Skeleton skeleton;
        private readonly SpineHeroSword sword;
        private readonly SkeletonClipping clipper=new SkeletonClipping();
        private readonly Vector2 bodyPage, weaponPage;

        internal SpinePoseSource(string name,string path,string initialSkin=null,string effectClip=null,
            string loopClip=null,string attackClip=null)
        {
            bool hero=name=="Hero";
            var data=Load(path,"Skeleton","AtlasData",out bodyPage);
            skeleton=new Skeleton(data);
            if(hero)
            {
                var skin=new Skin("body-and-hands");
                skin.AddSkin(data.FindSkin("Body"));
                skin.AddSkin(data.FindSkin("Hand"));
                skeleton.SetSkin(skin);
                var weapon=Load(path,"WeaponSkeleton","WeaponAtlas",out weaponPage);
                sword=new SpineHeroSword((RegionAttachment)weapon.FindSkin("LiuLangChangJian").Attachments.Single().Attachment);
            }
            else if(!string.IsNullOrEmpty(initialSkin))
                skeleton.SetSkin(data.FindSkin(initialSkin)??throw new InvalidDataException($"Missing Spine skin {initialSkin}: {path}"));

            Spine.Animation Find(string clip) => data.FindAnimation(clip) ??
                data.Animations.FirstOrDefault(a=>string.Equals(a.Name,clip,StringComparison.OrdinalIgnoreCase));
            if(effectClip!=null)
            {
                var effect=Find(effectClip)??throw new InvalidDataException($"Missing Spine effect {effectClip}: {path}");
                clips=new Dictionary<string,Spine.Animation>{{"effect",effect}};
                if(loopClip!=null)
                    clips["loop"]=Find(loopClip)??throw new InvalidDataException($"Missing Spine loop {loopClip}: {path}");
                return;
            }
            var idle=Find("Idle");
            clips=new Dictionary<string,Spine.Animation>
            {
                ["idle"]=idle,["walk"]=Find("Run")??Find("Move")??idle,
                ["attack"]=Find(attackClip??(hero?"Battle/Attack_Sword":"Attack")),
                ["hurt"]=Find("Hit"),["down"]=Find("Death")
            };
            if(hero)
            {
                clips["cast"]=Find("Battle/Skill_Sword")??clips["attack"];
                if(Find("Run_to_Idle") is Spine.Animation dash) clips["dash"]=dash;
                if(Find("Battle/Cast_Loop") is Spine.Animation sailApproach && sailApproach.Duration>0)
                    clips["sailApproach"]=sailApproach;
            }
            if(clips.Values.Any(clip=>clip==null || clip.Duration<=0))
                throw new InvalidDataException($"Missing or empty Spine battle clip: {path}");
        }

        internal float Duration(string clip) => clips[clip].Duration;
        internal bool HasClip(string clip) => clips.ContainsKey(clip);

        internal (float center,float ground,float width,float height) Bounds(string clip="idle",float time=0)
        {
            Apply(clip,time);
            float[] buffer=null;
            skeleton.GetBounds(out float x,out float y,out float width,out float height,ref buffer);
            return (x+width*.5f,y,width,height);
        }

        private static SkeletonData Load(string path,string skeleton,string atlas,out Vector2 size)
        {
            string key=path+skeleton;
            if(DataCache.TryGetValue(key,out var cached))
            {
                size=cached.size;
                return cached.data;
            }
            var text=Resources.Load<TextAsset>(path+atlas);
            var binary=Resources.Load<TextAsset>(key);
            if(text==null || binary==null) throw new InvalidDataException($"Missing Spine assets: {path}");
            using var reader=new StringReader(text.text);
            using var stream=new MemoryStream(binary.bytes,false);
            var sourceAtlas=new Atlas(reader,"",new AtlasPages());
            var page=sourceAtlas.Pages.Single();
            size=new Vector2(page.width,page.height);
            SkeletonData data;
            if(path=="SpineActors/EventBox/")
            {
                using var json=new StreamReader(stream);
                data=new SkeletonJson(new AtlasAttachmentLoader(sourceAtlas)).ReadSkeletonData(json);
                if(data.Version!="4.0.63") throw new InvalidDataException($"Unexpected Spine version {data.Version}");
            }
            else data=Parse(sourceAtlas,stream);
            DataCache.Add(key,(data,size));
            return data;
        }

        private static SkeletonData Parse(Atlas atlas,Stream stream)
        {
            var data=new SkeletonBinary(new AtlasAttachmentLoader(atlas)).ReadSkeletonData(stream);
            if(data.Version!="4.0.63") throw new InvalidDataException($"Unexpected Spine version {data.Version}");
            return data;
        }

        private sealed class AtlasPages : TextureLoader
        {
            public void Load(AtlasPage page,string path) {}
            public void Unload(object obj) {}
        }

        private void Apply(string name,float time)
        {
            skeleton.SetToSetupPose();
            clips[name].Apply(skeleton,-1,time,false,null,1,MixBlend.Setup,MixDirection.In);
            skeleton.UpdateWorldTransform();
        }

        internal ActorAnimationGraphic.Frame Evaluate(string name,float time,bool premultiply=false)
        {
            Apply(name,time);
            var vertices=new List<UIVertex>();
            var indices=new List<int>();
            var weaponPose=sword?.Pose(name,time,Duration(name))??default;
            float sheetWidth=bodyPage.x+weaponPage.x;
            float sheetHeight=Math.Max(bodyPage.y,weaponPage.y);
            foreach(var slot in skeleton.DrawOrder)
            {
                if(!slot.Bone.Active) { clipper.ClipEnd(slot); continue; }
                bool weapon=slot.Data.Name==weaponPose.slot;
                var attachment=weapon?weaponPose.attachment:slot.Attachment;
                if(name=="dash" && (slot.Data.Name.Contains("FX_Smoke_B") ||
                    attachment?.Name.Contains("FX_Smoke_B")==true))
                {
                    clipper.ClipEnd(slot);
                    continue;
                }
                if(attachment is ClippingAttachment clipping) { clipper.ClipStart(slot,clipping); continue; }

                if(slot.A>0)
                {
                    if(attachment is RegionAttachment region)
                    {
                        var xy=new float[8];
                        region.ComputeWorldVertices(slot.Bone,xy,0,2);
                        Append(xy,region.UVs,new[]{0,1,2,2,3,0},
                            new Color(slot.R*region.R,slot.G*region.G,slot.B*region.B,slot.A*region.A),weapon,
                            slot.Data.BlendMode);
                    }
                    else if(attachment is MeshAttachment mesh)
                    {
                        var xy=new float[mesh.WorldVerticesLength];
                        mesh.ComputeWorldVertices(slot,0,xy.Length,xy,0,2);
                        Append(xy,mesh.UVs,mesh.Triangles,
                            new Color(slot.R*mesh.R,slot.G*mesh.G,slot.B*mesh.B,slot.A*mesh.A),false,
                            slot.Data.BlendMode);
                    }
                }
                clipper.ClipEnd(slot);
            }
            clipper.ClipEnd();
            return new ActorAnimationGraphic.Frame(vertices.ToArray(),indices.ToArray());

            void Append(float[] xy,float[] uv,int[] triangles,Color tint,bool isWeapon,BlendMode blend)
            {
                if(tint.a<=0) return;
                if(premultiply)
                {
                    tint.r*=tint.a;
                    tint.g*=tint.a;
                    tint.b*=tint.a;
                    if(blend==BlendMode.Additive) tint.a=0;
                }
                if(clipper.IsClipping)
                {
                    clipper.ClipTriangles(xy,xy.Length,triangles,triangles.Length,uv);
                    xy=clipper.ClippedVertices.ToArray();
                    uv=clipper.ClippedUVs.ToArray();
                    triangles=clipper.ClippedTriangles.ToArray();
                }
                var page=isWeapon?weaponPage:bodyPage;
                float pageX=isWeapon?bodyPage.x:0;
                int first=vertices.Count;
                for(int i=0;i<xy.Length;i+=2)
                {
                    var v=UIVertex.simpleVert;
                    v.position=new Vector3(xy[i],xy[i+1],0);
                    v.uv0=new Vector2((pageX+uv[i]*page.x)/sheetWidth,1-uv[i+1]*page.y/sheetHeight);
                    v.color=tint;
                    vertices.Add(v);
                }
                foreach(int index in triangles) indices.Add(first+index);
            }
        }
    }
}
