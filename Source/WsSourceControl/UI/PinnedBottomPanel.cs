using System;
using FlaxEngine;
using FlaxEngine.GUI;

namespace WsSourceControl.UI
{
    public class PinnedBottomPanel : ContainerControl
    {
        public float PinHeight = 40f;
        public Action<PinnedBottomPanel> LayoutChildren;

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);
            if (Parent != null)
                Bounds = new Rectangle(0f, Parent.Height - PinHeight, Parent.Width, PinHeight);
            LayoutChildren?.Invoke(this);
        }
    }
}
