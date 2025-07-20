using RimWorld;
using System.Collections.Generic;
using Verse;

namespace WorkOnThis.Windows
{
    public class PositionedFloatMenu : FloatMenu
    {
        private float x, y;

        public PositionedFloatMenu(List<FloatMenuOption> options, Thing target, float x, float y) : base(options, target.LabelCap)
        {
            this.x = x;
            this.y = y;
        }

        public override void PostOpen()
        {
            base.PostOpen();
            windowRect.x = x;
            windowRect.y = y;
        }
    }
}
