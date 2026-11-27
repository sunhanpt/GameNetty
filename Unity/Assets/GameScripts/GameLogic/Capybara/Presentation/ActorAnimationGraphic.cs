using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace CapybaraRebuild
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ActorAnimationGraphic : MaskableGraphic
    {
        [SerializeField] private float uiEffectScale=1;
        internal float UiEffectScale => uiEffectScale;

        [Serializable]
        private sealed class EnemyRigSettings
        {
            public string skin=null, attack=null;
        }

        internal sealed class Frame
        {
            public readonly UIVertex[] vertices;
            public readonly int[] indices;

            public Frame(UIVertex[] vertices,int[] indices)
            {
                this.vertices=vertices;
                this.indices=indices;
            }
        }

        private sealed class Clip
        {
            public float duration, cue;
            public Frame[] frames;
        }

        private sealed class Rig
        {
            public float center, ground, width, height;
            public Texture2D atlas;
            public readonly Dictionary<string,Clip> clips=new Dictionary<string,Clip>(StringComparer.Ordinal);
        }

        private static readonly Dictionary<string,Rig> Cache=new Dictionary<string,Rig>(StringComparer.Ordinal);
        private Rig rig;
        private Frame frame;
        private string currentClip;
        private bool mirror;
        private float layoutScale;
        private Material effectMaterial;
        private SpinePoseSource live;
        private float currentTime=-1;

        public override Texture mainTexture => rig?.atlas ?? base.mainTexture;

        internal float SourceAspect => rig.width/rig.height;

        internal bool LoadEnemy(int modelId)
        {
            // Keep the original mage's sampled cast cue and fallback rig.
            if(modelId==220081) return Load("Mage");
            if(modelId<=0) return false;
            string path="EnemyRigs/"+modelId+"/";
            var atlas=Resources.Load<Texture2D>(path+"RigAtlas");
            if(atlas==null) return false;
            try
            {
                var settingsAsset=Resources.Load<TextAsset>(path+"RigSettings");
                var settings=settingsAsset!=null?JsonUtility.FromJson<EnemyRigSettings>(settingsAsset.text):null;
                live=new SpinePoseSource("Enemy",path,settings?.skin??(modelId==230031?"Hei":null),
                    attackClip:settings?.attack);
                var bounds=live.Bounds();
                if(!(bounds.width>0) || !(bounds.height>0))
                    throw new InvalidDataException("The original idle pose has no visible geometry.");
                rig=new Rig{center=bounds.center,ground=bounds.ground,width=bounds.width,height=bounds.height,atlas=atlas};
                mirror=true;
                raycastTarget=false;
                SetPose("idle",0,true);
                SetMaterialDirty();
                return true;
            }
            catch(InvalidDataException e)
            {
                Debug.LogWarning($"Unable to load enemy model {modelId}: {e.Message}");
            }
            return false;
        }

        internal bool Load(string name)
        {
            if(name!="Hero" && name!="Mage") return false;
            mirror=name=="Mage";
            string path="SpineActors/"+name+"/";
            if(!Cache.TryGetValue(name,out rig))
            {
                var data=Resources.Load<TextAsset>(path+"Rig");
                var atlas=Resources.Load<Texture2D>(path+"RigAtlas");
                if(data==null || atlas==null) return false;
                try
                {
                    rig=Read(data.bytes,atlas);
                    Cache.Add(name,rig);
                }
                catch(InvalidDataException e)
                {
                    Debug.LogWarning($"Unable to load actor animation {name}: {e.Message}");
                    return false;
                }
            }
            live=new SpinePoseSource(name,path);
            raycastTarget=false;
            SetPose("idle",0,true);
            SetMaterialDirty();
            return true;
        }

        internal bool LoadEffect(string name)
        {
            string path="SpineActors/"+name+"/";
            var atlas=Resources.Load<Texture2D>(path+"RigAtlas");
            if(atlas==null) return false;
            try
            {
                live=new SpinePoseSource(name,path,effectClip:name);
                var bounds=live.Bounds("effect",live.Duration("effect")*.375f);
                if(bounds.width<=0 || bounds.height<=0) return false;
                rig=new Rig{center=bounds.center,ground=bounds.ground,width=bounds.width,height=bounds.height,atlas=atlas};
                raycastTarget=false;
                SetPose("effect",0,false);
                SetMaterialDirty();
                return true;
            }
            catch(InvalidDataException exception)
            {
                Debug.LogWarning($"Unable to load Spine effect {name}: {exception.Message}");
            }
            return false;
        }

        internal bool LoadUiEffect(string name,string opening,string loop,float scale,string skin=null)
        {
            string path="SpineActors/"+name+"/";
            var atlas=Resources.Load<Texture2D>(path+"RigAtlas");
            var shader=Resources.Load<Shader>("Reference/AdventureVictory/SpinePremultiplied");
            if(atlas==null || shader==null) return false;
            try
            {
                live=new SpinePoseSource(name,path,skin,effectClip:opening,loopClip:loop);
                layoutScale=scale;
                rig=new Rig{atlas=atlas};
                effectMaterial=new Material(shader);
                material=effectMaterial;
                raycastTarget=false;
                SetPose("effect",0,false);
                SetMaterialDirty();
                return true;
            }
            catch(InvalidDataException exception)
            {
                Debug.LogWarning($"Unable to load Spine UI effect {name}: {exception.Message}");
            }
            return false;
        }

        protected override void OnDestroy()
        {
            if(effectMaterial!=null) Destroy(effectMaterial);
            base.OnDestroy();
        }

        internal bool HasClip(string name) => live.HasClip(name);

        internal float Duration(string name) => live.Duration(name);

        internal float Cue(string name) => rig.clips.TryGetValue(name,out var clip)?clip.cue:-1;

        internal void SetPose(string name,float time,bool loop)
        {
            float duration=Duration(name);
            float phase=loop?Mathf.Repeat(time,duration):Mathf.Min(time,duration);
            if(currentClip==name && Mathf.Approximately(currentTime,phase)) return;
            currentTime=phase;
            frame=live.Evaluate(name,phase,layoutScale>0);
            currentClip=name;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if(frame==null) return;
            var rect=rectTransform.rect;
            bool authoredPosition=layoutScale>0;
            float scale=authoredPosition?layoutScale:rect.width/rig.width;
            float originX=authoredPosition?rect.center.x:rect.center.x-rig.center*scale*(mirror?-1:1);
            float originY=authoredPosition?rect.center.y:rect.yMin-rig.ground*scale;
            foreach(var vertex in frame.vertices)
            {
                var v=vertex;
                v.position=new Vector3(originX+v.position.x*scale*(mirror?-1:1),originY+v.position.y*scale,0);
                v.color=(Color)v.color*color;
                mesh.AddVert(v);
            }
            for(int i=0;i<frame.indices.Length;i+=3)
                mesh.AddTriangle(frame.indices[i],frame.indices[i+1],frame.indices[i+2]);
        }

        private static Rig Read(byte[] bytes,Texture2D atlas)
        {
            try
            {
                using var reader=new BinaryReader(new MemoryStream(bytes,false));
                if(reader.ReadUInt32()!=0x31475243) throw new InvalidDataException("Wrong CRG1 format.");
                var rig=new Rig{center=reader.ReadSingle(),ground=reader.ReadSingle(),width=reader.ReadSingle(),
                    height=reader.ReadSingle(),atlas=atlas};
                int clipCount=reader.ReadByte();
                if(clipCount!=5 || rig.width<=0 || rig.height<=0) throw new InvalidDataException("Invalid dimensions or clip count.");
                for(int j=0;j<clipCount;j++)
                {
                    int length=reader.ReadByte();
                    string name=Encoding.ASCII.GetString(reader.ReadBytes(length));
                    float duration=reader.ReadSingle();
                    float cue=reader.ReadSingle();
                    int samples=reader.ReadUInt16();
                    if(samples<2 || samples>1000 || !(duration>0))
                        throw new InvalidDataException($"Invalid clip {name}.");
                    var frames=new Frame[samples];
                    for(int i=0;i<samples;i++)
                    {
                        var vertices=new UIVertex[reader.ReadUInt16()];
                        var indices=new int[reader.ReadUInt16()];
                        if(indices.Length%3!=0) throw new InvalidDataException("Incomplete triangle.");
                        for(int k=0;k<vertices.Length;k++)
                        {
                            var v=UIVertex.simpleVert;
                            v.position=new Vector3(reader.ReadSingle(),reader.ReadSingle(),0);
                            v.uv0=new Vector2(reader.ReadSingle(),reader.ReadSingle());
                            v.color=new Color32(reader.ReadByte(),reader.ReadByte(),reader.ReadByte(),reader.ReadByte());
                            vertices[k]=v;
                        }
                        for(int k=0;k<indices.Length;k++)
                        {
                            indices[k]=reader.ReadUInt16();
                            if(indices[k]>=vertices.Length) throw new InvalidDataException("Index exceeds frame vertex count.");
                        }
                        frames[i]=new Frame(vertices,indices);
                    }
                    rig.clips.Add(name,new Clip{duration=duration,cue=cue,frames=frames});
                }
                if(reader.BaseStream.Position!=bytes.Length) throw new InvalidDataException("Unexpected trailing animation data.");
                return rig;
            }
            catch(EndOfStreamException e)
            {
                throw new InvalidDataException("Truncated actor animation.",e);
            }
        }
    }
}
