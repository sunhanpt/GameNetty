using System;
using Spine;

namespace CapybaraRebuild
{
    internal sealed class SpineHeroSword
    {
        private readonly RegionAttachment back, hand;

        internal SpineHeroSword(RegionAttachment sword)
        {
            back=Place(sword,-35,0,-160);
            hand=Place(sword,80,0,0,80);
        }

        internal (string slot,RegionAttachment attachment) Pose(string clip,float time,float duration) =>
            (clip=="attack" || clip=="cast") && time>=.055f && time<duration-.085f ? ("Jian2",hand) : ("Jian",back);

        // The weapon atlas is attached to the player rig's back and hand sockets.
        // These local offsets align its hilt to those sockets in the reference poses.
        private static RegionAttachment Place(RegionAttachment source,float angle,float x,float y,float grip=0)
        {
            var copy=(RegionAttachment)source.Copy();
            float radians=angle*(MathF.PI/180);
            float sin=MathF.Sin(radians), cos=MathF.Cos(radians);
            copy.X=source.X*cos-(source.Y+grip)*sin+x;
            copy.Y=source.X*sin+(source.Y+grip)*cos+y;
            copy.Rotation=source.Rotation+angle;
            copy.UpdateOffset();
            return copy;
        }
    }
}
