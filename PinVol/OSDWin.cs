using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
//using System.Windows;
using System.Windows.Forms;

namespace PinVol
{
    public partial class OSDWin : Form
    {
        public enum OSDType
        {
            None,
            Local,      // local table setting for default audio device
            Local2,     // local table setting for secondary audio device
            Global,      // global volume setting
            SSFBG,         // SSF Back Glass volume setting
            SSFRS,         // SSF Rear Sides volume setting
            SSFFS,         // SSF Front Sides volume setting
        };

        public OSDWin(UIWin mainwin)
        {
            this.mainwin = mainwin;
            InitializeComponent();

            // set up drawing objects for the label text
            font = new Font(SystemFonts.CaptionFont.FontFamily, 24.0f);
            titleFmt = new StringFormat(StringFormatFlags.NoWrap);
            titleFmt.Alignment = StringAlignment.Center;
            titleFmt.LineAlignment = StringAlignment.Far;

            // set the night mode icon transparency
            nightMode.MakeTransparent(nightMode.GetPixel(0, 0));
            //lockedVol.MakeTransparent(lockedVol.GetPixel(0, 0));
        }

        // the main window (source of the current volume level)
        UIWin mainwin;

        // show without activation
        protected override bool ShowWithoutActivation { 
            get { return true; } 
        }

        // setup mode
        bool setupMode = false;
        Color origTransparencyKey;
        public bool InSetup() { return setupMode; }
        public void Setup(bool f)
        {
            // do nothing if already in the desired mode
            if (setupMode == f)
                return;

            // set the new mode
            if (f)
            {
                // turn off transparency
                origTransparencyKey = TransparencyKey;
                TransparencyKey = Color.Empty;

                // turn the borders on for sizing and positioning
                FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;

                // move to account for the border controls
                Point zero = PointToScreen(new Point(0, 0));
                Location = new Point(Location.X - (zero.X - Location.X), Location.Y - (zero.Y - Location.Y));

                // show the setup controls
                btnDone.Visible = true;
                btnCCW.Visible = true;
                btnCW.Visible = true;

                // show the window
                Opacity = 0.85f;
                Visible = true;
                Enabled = true;

                // switch to local volume display, since that uses the larger text caption
                mainwin.osdType = OSDType.Local;

                // flag that we're now in setup mode
                setupMode = true;
            }
            else
            {
                // we're no longer in setup mode
                setupMode = false;

                // restore transparency
                TransparencyKey = origTransparencyKey;

                // hide the setup controls
                btnDone.Visible = false;
                btnCCW.Visible = false;
                btnCW.Visible = false;

                // disable the whole window so it won't accidentally grab focus
                Enabled = false;

                // move to account for the the disappearing border controls
                Point zero = PointToScreen(new Point(0, 0));
                Location = new Point(Location.X + (zero.X - Location.X), Location.Y + (zero.Y - Location.Y));

                // turn off the border
                FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;

                // fade the display
                mainwin.BeginFadeOSD();
            }
        }

        private void VolumeOverlay_Paint(object sender, PaintEventArgs e)
        {
            // get the graphics context
            Graphics gr = e.Graphics;

            // rotate the coordinate system
            int r = mainwin.cfg.OSDRotation;
            switch (r)
            {
                case 0: gr.TranslateTransform(0, 0); break;
                case 90: gr.TranslateTransform(0, ClientSize.Height); break;
                case 180: gr.TranslateTransform(ClientSize.Width, ClientSize.Height); break;
                case 270: gr.TranslateTransform(ClientSize.Width, 0); break;
            }
            gr.RotateTransform(-r);

            // note in the title if muted
            String muted = mainwin.globalMute ? " (Muted)" : "";

            // draw the selected volume level
            switch (mainwin.osdType)
            {
                case OSDType.Local:
                    DrawOsd(gr,
                        "<< " + mainwin.appmon.FriendlyName + " >>\nTable Volume   " 
                        + Math.Round(mainwin.localVolume * 100) + "%" + muted,
                        mainwin.localVolume, r);
                    break;

                case OSDType.Local2:
                    DrawOsd(gr,
                        "<< " + mainwin.appmon.FriendlyName + " >>\nTable Volume (Secondary)   "
                        + Math.Round(mainwin.local2Volume * 100) + "%" + muted,
                        mainwin.local2Volume, r);
                    break;

                case OSDType.Global:
                case OSDType.None:
                    var v = mainwin.globalVolume[(int)mainwin.volumeMode];
                    DrawOsd(gr,
                        "Global Volume   " + Math.Round(v * 100) + "%" + muted, v, r);
                    break;

                case OSDType.SSFBG:
                    DrawOsd(gr,
                        "<< " + mainwin.appmon.FriendlyName + " >>\nSSF Back Glass Gain   "
                        + Math.Round(mainwin.SSFBGVolume) + "db" + muted,
                        mainwin.SSFBGVolume, r);
                    break;

                case OSDType.SSFRS:
                    DrawOsd(gr,
                        "<< " + mainwin.appmon.FriendlyName + " >>\nSSF Rear Exciters Gain   "
                        + Math.Round(mainwin.SSFRSVolume) + "db" + muted,
                        mainwin.SSFRSVolume, r);
                    break;

                case OSDType.SSFFS:
                    DrawOsd(gr,
                        "<< " + mainwin.appmon.FriendlyName + " >>\nSSF Front Exciters Gain   "
                        + Math.Round(mainwin.SSFFSVolume) + "db" + muted,
                        mainwin.SSFFSVolume, r);
                    break;
            }
        }

        private struct canvasStates
        {
            public SizeF scaleRef;           // a value to reference for sizing decisions, with X and Y scales
            public Rectangle windowRect;     // the OSD form's drawable client area
            public Rectangle bgRect; // the area to be the shadow background of the OSD
            public Rectangle osdInfoRect;    // an area smaller than the backgroudnRect to allow an border / padding for the OSD
            public Rectangle volumeBarRect;  // area to contain the drawing of the volume bar
            public Rectangle textRect;       // area for the display text (multi-line)
            public Rectangle icon1Rect;
            public Rectangle icon2Rect;
            public string text;

            public Brush bgBrush;
            
            volatile bool windowResized;
            volatile bool textChanged;
            //public void SetWindowSize(Rectangle r) { windowRect = r; windowResized = true; }
            public void SetText(string str) 
            {
                text = str;
                textChanged = true;
            }
        }

        canvasStates j;


        void DrawBg(Graphics gr)
        {
            gr.FillRectangle(j.bgBrush, j.bgRect);
        }

        void DrawOsdInfo(Graphics gr)
        {
            Pen p = new Pen(Color.FromArgb(200, Color.Yellow));
            p.DashStyle = DashStyle.Dash;
            gr.DrawRectangle(p, j.osdInfoRect);

        }

        void DrawVolumeBar(Graphics gr)
        {
            Pen p = new Pen(Color.FromArgb(200, Color.Lime));
            p.DashStyle = DashStyle.Solid;

            HatchBrush b = new HatchBrush(HatchStyle.DarkVertical, Color.FromArgb(200, Color.LimeGreen));
            gr.FillRectangle(b, j.volumeBarRect);
            gr.DrawRectangle(p, j.volumeBarRect);
        }

        void DrawText(Graphics gr)
        {
            Pen p = new Pen(Color.FromArgb(200, Color.LightBlue));
            p.DashStyle = DashStyle.DashDot;
            gr.DrawRectangle(p, j.textRect);
        }
        void DrawIconLayout(Graphics gr, Rectangle r)
        {
            Pen p = new Pen(Color.FromArgb(200, Color.Blue));
            p.DashStyle = DashStyle.DashDot;
            HatchBrush b = new HatchBrush(HatchStyle.LargeConfetti, Color.FromArgb(200, Color.Blue));
            gr.FillRectangle(b, r);
            gr.DrawRectangle(p, r);
        }

        void DrawLayout(Graphics gr, canvasStates c)
        {
            //DrawBg(gr);
            DrawOsdInfo(gr);
            DrawVolumeBar(gr);
            DrawText(gr);
            DrawIconLayout(gr, j.icon1Rect);
            DrawIconLayout(gr, j.icon2Rect);
        }

        

        void DrawOsd(Graphics gr, String title, float vol, int r)
        {
            // handle window rotation affects on width & height: 
            // (even though graphics calls were transformed to account for the rotation, the
            // Windows axes are not affected, making Width and Height reversed in some cases.)
            j.windowRect = ClientRectangle;
            if (r == 90 || r == 270)
            {
                j.windowRect.Width = ClientRectangle.Height;
                j.windowRect.Height = ClientRectangle.Width;
            }

            const int buttonMargin = 8;
            const float iconScale = 0.9f;

            j.bgBrush = new SolidBrush(Color.FromArgb(0, 0, 0));

            // initialize the canvas states
            j.scaleRef = gr.MeasureString("X", font);

            Size osdPadding = new Size((int)(j.scaleRef.Width / -2.0f), (int)(j.scaleRef.Height / -4.0f));


            // calculated padded space of OSD Info rect
            j.osdInfoRect = j.windowRect;
            j.osdInfoRect.Inflate(osdPadding);

            // set the volume bar rect
            j.volumeBarRect = new Rectangle(0, 0, (int)j.osdInfoRect.Width, (int)(j.scaleRef.Height * 1.5f));
            AlignRect(j.osdInfoRect, ref j.volumeBarRect, AlignMode.HCenter);
            AlignRect(j.osdInfoRect, ref j.volumeBarRect, AlignMode.Bottom);

            // get the text rect
            j.SetText(title);
            SizeF titlesz = gr.MeasureString(title, font);
            j.textRect.Size = Size.Ceiling(titlesz);

            // position the text rect above volume bar
            AlignRect(j.osdInfoRect, ref j.textRect, AlignMode.HCenter);
            AlignRect(j.volumeBarRect, ref j.textRect, AlignMode.Above, 8);

            // determine the bgRect based on top of text rect.
            j.bgRect = j.windowRect;  //todo: Shoud not be the window rect.


            // make osdInfoRect (padding rect) the smaller bgRect
            j.icon1Rect.Width = j.icon1Rect.Height = (int)Math.Floor(j.scaleRef.Height * iconScale);
            j.icon2Rect.Width = j.icon2Rect.Height = (int)Math.Floor(j.scaleRef.Height * iconScale);
            AlignRect(j.textRect, ref j.icon1Rect, AlignMode.VCenter);
            AlignRect(j.textRect, ref j.icon2Rect, AlignMode.VCenter);
            AlignRect(j.textRect, ref j.icon1Rect, AlignMode.LeftOf, buttonMargin);
            AlignRect(j.textRect, ref j.icon2Rect, AlignMode.RightOf, buttonMargin);

            // place icon1Rect and icon2Rect relative to textRect

            // draw the underlying background
            DrawBg(gr);

            // draw the OSD layout
            //DrawLayout(gr, j);

            //DrawVolumeBar(gr, title, vol, r);
            NewDrawVolumeBar(gr, title, vol, r, j.volumeBarRect);
        }

        void NewDrawVolumeBar(Graphics gr, String title, float vol, int r, Rectangle rect)
        {
            // Get the window size.  If rotated 90 or 270 degrees, swap width
            // and height for our bar size calculations.
            //Size winsz = ClientSize;
            Size winsz = rect.Size;
            //if (r == 90 || r == 270)
            //{
            //    //winsz.Width = ClientSize.Height;
            //    //winsz.Height = ClientSize.Width;
            //    winsz.Width = rect.Height;
            //    winsz.Height = rect.Width;
            //}


            // Figure the bar height, based on the text height
            SizeF txtsz = gr.MeasureString("X", font);
            //Size barsz = new Size(winsz.Width, (int)(txtsz.Height * 1.5f));
            Size barsz = rect.Size;

            // Figure the bar top position, aligning at the bottom
            //int left = 0, right = barsz.Width;
            //int top = winsz.Height - barsz.Height;
            int left = rect.Left;
            int right = rect.Right;
            int top = rect.Top;
            int bottom = rect.Bottom;

            // figure the volume bar width in pixels
            int volwid = (int)(barsz.Width * vol);

            // Start with a default tick width, then refigure so that we fit an
            // integral number (or as close as possible) into the available width.
            int tickwid = 30, ticksp = 10;
            int availwid = winsz.Width - tickwid;
            int nticks = Math.Max(availwid / (tickwid + ticksp), 1);
            tickwid = Math.Max((availwid / nticks) - ticksp, 1);

            // select the brushes
            Brush black = Brushes.Black;
            Brush rcbrush, txbrush;
            switch (mainwin.osdType)
            {
                case OSDType.Local:
                    rcbrush = txbrush = Brushes.Lime;
                    break;

                case OSDType.Local2:
                    rcbrush = txbrush = Brushes.Violet;
                    break;

                case OSDType.SSFBG:
                case OSDType.SSFRS:
                case OSDType.SSFFS:
                    rcbrush = txbrush = Brushes.White;
                    break;

                case OSDType.Global:
                case OSDType.None:
                default:
                    if (mainwin.volumeMode == UIWin.VolumeMode.Night)
                        rcbrush = txbrush = Brushes.Blue;
                    else
                        rcbrush = txbrush = Brushes.DeepSkyBlue;
                    break;
            }

            // create an outline pen if needed
            bool mute = mainwin.globalMute;
            int penwid = 2;
            using (Pen rcpen = mute ? new Pen(rcbrush, penwid) : null)
            {
                // draw the ticks
                for (int x = left; x < right; x += tickwid + ticksp)
                {
                    // get the area of this tick
                    Rectangle rc;
                    Rectangle halftick = new Rectangle(x + tickwid / 4, top + barsz.Height / 4, tickwid / 2, barsz.Height / 2);
                    bool drawhalf = false;
                    if (x + tickwid <= volwid)
                    {
                        // we're still below the volume level, so draw a whole tick
                        //rc = new Rectangle(x, top, tickwid, top + barsz.Height);
                        rc = new Rectangle(x, top, tickwid, barsz.Height);
                    }
                    else if (x < volwid)
                    {
                        // this tick is partially within the volume level, so draw a portion
                        // of the tick plus the small tick
                        //rc = new Rectangle(x, top, volwid - x, top + barsz.Height);
                        rc = new Rectangle(x, top, volwid - x, barsz.Height);
                        if (rc.Right > halftick.Left)
                            halftick = new Rectangle(rc.Right, halftick.Top, halftick.Right - rc.Right, halftick.Height);
                        drawhalf = true;
                    }
                    else
                    {
                        // We're entirely beyond the volume level.  Draw a half tick.
                        rc = halftick;
                    }

                    // draw the rectangle or outline
                    if (mute)
                        gr.DrawRectangle(rcpen, rc);
                    else
                        gr.FillRectangle(rcbrush, rc);

                    // if we have a partial tick, also draw the half tick
                    if (drawhalf)
                    {
                        if (mute)
                            gr.DrawRectangle(rcpen, halftick);
                        else
                            gr.FillRectangle(rcbrush, halftick);
                    }
                }
            }

            // draw the title overlay
            float tx = left + (right - left) / 2;
            float ty = top - 8;
            gr.DrawString(title, font, black, tx + 1, ty + 1, titleFmt);    // shadow
            gr.DrawString(title, font, txbrush, tx, ty, titleFmt);          // main text

            // add the night mode icon if appropriate
            if (mainwin.volumeMode == UIWin.VolumeMode.Night)
            {
                SizeF titlesz = gr.MeasureString(title, font);
                SizeF linesz = gr.MeasureString("X", font);
                float psz = linesz.Height * .8f;
                float px = tx - titlesz.Width / 2 - 8 - psz;
                float py = ty - titlesz.Height / 2 - psz / 2;
                gr.DrawImage(nightMode, new PointF[] {
                    new PointF(px, py),
                    new PointF(px + psz, py),
                    new PointF(px, py + psz)
                });

                // draw the lock icon next to the level if the requested volume control is currently restricted by a lock
                if (mainwin.cfg.NightVolLock && (mainwin.osdType == OSDType.Global))
                {
                    float drawWidth = psz;  //Match dimensions of nightMode icon
                    float drawHeight = psz; //Match dimensions of nightMode icon.
                    float titleMarginH = 8f;
                    PointF lockedBmpOrigin = new PointF(tx + titlesz.Width / 2 + titleMarginH, py);
                    gr.DrawImage(lockedVol, new PointF[]
                    {
                        lockedBmpOrigin,
                        new PointF(lockedBmpOrigin.X + drawWidth, lockedBmpOrigin.Y),
                        new PointF(lockedBmpOrigin.X, lockedBmpOrigin.Y + drawHeight)
                    });
                }
            }


        }

        void DrawVolumeBar(Graphics gr, String title, float vol, int r)
        {
            // Get the window size.  If rotated 90 or 270 degrees, swap width
            // and height for our bar size calculations.
            Size winsz = ClientSize;
            if (r == 90 || r == 270)
            {
                winsz.Width = ClientSize.Height;
                winsz.Height = ClientSize.Width;
            }

            // Figure the bar height, based on the text height
            SizeF txtsz = gr.MeasureString("X", font);
            Size barsz = new Size(winsz.Width, (int)(txtsz.Height * 1.5f));

            // Figure the bar top position, aligning at the bototm
            int left = 0, right = barsz.Width;
            int top = winsz.Height - barsz.Height;

            // figure the volume bar width in pixels
            int volwid = (int)(barsz.Width * vol);

            // Start with a default tick width, then refigure so that we fit an
            // integral number (or as close as possible) into the available width.
            int tickwid = 30, ticksp = 10;
            int availwid = winsz.Width - tickwid;
            int nticks = Math.Max(availwid / (tickwid + ticksp), 1);
            tickwid = Math.Max((availwid / nticks) - ticksp, 1);

            // select the brushes
            Brush black = Brushes.Black;
            Brush rcbrush, txbrush;
            switch (mainwin.osdType)
            {
                case OSDType.Local:
                    rcbrush = txbrush = Brushes.Lime;
                    break;

                case OSDType.Local2:
                    rcbrush = txbrush = Brushes.Violet;
                    break;

                case OSDType.SSFBG:
                case OSDType.SSFRS:
                case OSDType.SSFFS:
                    rcbrush = txbrush = Brushes.White;
                    break;

                case OSDType.Global:
                case OSDType.None:
                default:
                    if (mainwin.volumeMode == UIWin.VolumeMode.Night)
                        rcbrush = txbrush = Brushes.Blue;
                    else
                        rcbrush = txbrush = Brushes.DeepSkyBlue;
                    break;
            }

            // create an outline pen if needed
            bool mute = mainwin.globalMute;
            int penwid = 2;
            using (Pen rcpen = mute ? new Pen(rcbrush, penwid) : null)
            {
                // draw the ticks
                for (int x = left; x < right; x += tickwid + ticksp)
                {
                    // get the area of this tick
                    Rectangle rc;
                    Rectangle halftick = new Rectangle(x + tickwid / 4, top + barsz.Height / 4, tickwid / 2, barsz.Height / 2);
                    bool drawhalf = false;
                    if (x + tickwid <= volwid)
                    {
                        // we're still below the volume level, so draw a whole tick
                        rc = new Rectangle(x, top, tickwid, top + barsz.Height);
                    }
                    else if (x < volwid)
                    {
                        // this tick is partially within the volume level, so draw a portion
                        // of the tick plus the small tick
                        rc = new Rectangle(x, top, volwid - x, top + barsz.Height);
                        if (rc.Right > halftick.Left)
                            halftick = new Rectangle(rc.Right, halftick.Top, halftick.Right - rc.Right, halftick.Height);
                        drawhalf = true;
                    }
                    else
                    {
                        // We're entirely beyond the volume level.  Draw a half tick.
                        rc = halftick;
                    }

                    // draw the rectangle or outline
                    if (mute)
                        gr.DrawRectangle(rcpen, rc);
                    else
                        gr.FillRectangle(rcbrush, rc);

                    // if we have a partial tick, also draw the half tick
                    if (drawhalf)
                    {
                        if (mute)
                            gr.DrawRectangle(rcpen, halftick);
                        else
                            gr.FillRectangle(rcbrush, halftick);
                    }
                }
            }

            // draw the title overlay
            float tx = left + (right - left) / 2;
            float ty = top - 8;
            gr.DrawString(title, font, black, tx + 1, ty + 1, titleFmt);    // shadow
            gr.DrawString(title, font, txbrush, tx, ty, titleFmt);          // main text

            // add the night mode icon if appropriate
            if (mainwin.volumeMode == UIWin.VolumeMode.Night)
            {
                SizeF titlesz = gr.MeasureString(title, font);
                SizeF linesz = gr.MeasureString("X", font);
                float psz = linesz.Height * .8f;
                float px = tx - titlesz.Width/2 - 8 - psz;
                float py = ty - titlesz.Height/2 - psz/2;
                gr.DrawImage(nightMode, new PointF[] { 
                    new PointF(px, py), 
                    new PointF(px + psz, py),
                    new PointF(px, py + psz)
                });

                // draw the lock icon next to the level if the requested volume control is currently restricted by a lock
                if (mainwin.cfg.NightVolLock && (mainwin.osdType == OSDType.Global))
                {
                    float drawWidth = psz;  //Match dimensions of nightMode icon
                    float drawHeight = psz; //Match dimensions of nightMode icon.
                    float titleMarginH = 8f;
                    PointF lockedBmpOrigin = new PointF(tx + titlesz.Width/2 + titleMarginH, py);
                    gr.DrawImage(lockedVol, new PointF[]
                    {
                        lockedBmpOrigin,
                        new PointF(lockedBmpOrigin.X + drawWidth, lockedBmpOrigin.Y),
                        new PointF(lockedBmpOrigin.X, lockedBmpOrigin.Y + drawHeight)
                    });
                }
            }


        }

        // overlay font
        Font font;
        StringFormat titleFmt;

        // night mode image
        Bitmap nightMode = PinVol.Properties.Resources.nightModeLarge;
        Bitmap lockedVol = PinVol.Properties.Resources.lockedVolIcon;


        private void OSDWin_Resize(object sender, EventArgs e)
        {
            Invalidate();
            btnCCW.Top = ClientSize.Height - btnCCW.Height;
            btnCW.Top = ClientSize.Height - btnCW.Height;
            btnCW.Left = ClientSize.Width - btnCW.Width;
            btnDone.Left = (ClientSize.Width - btnDone.Width) / 2;
            btnDone.Top = (ClientSize.Height - btnDone.Height) / 2;

            SavePos();
        }

        private void OSDWin_Load(object sender, EventArgs e)
        {
            Rectangle r = mainwin.cfg.OSDPos;
            Location = new Point(r.Left, r.Top);
            ClientSize = new Size(r.Width, r.Height);
        }

        private void btnCCW_Click(object sender, EventArgs e)
        {
            RotateBy(90);
        }

        private void btnCW_Click(object sender, EventArgs e)
        {
            RotateBy(-90);
        }

        // Rotate by the given angle.  Note that we only support display 
        // rotations that are multiples of 90, so the delta must also be 
        // a multiple of 90.
        private void RotateBy(int dAngle)
        {
            // apply the delta, limiting to the 0-360 range
            mainwin.cfg.OSDRotation = ((mainwin.cfg.OSDRotation + dAngle) % 360 + 360) % 360;

            // note the config update
            mainwin.SetCfgDirty();

            // if rotating by +/- 90, swap the window's height and width
            if (Math.Abs(dAngle) % 180 == 90)
            {
                // note the current bottom location
                Point br = new Point(Right, Bottom);

                // rotate it
                int h = Height;
                Height = Width;
                Width = h;

                // The rotation controls are at the bottom of the window,
                // so it seems most intuitive to rotate around the bottom
                // right.  Reposition the window so that the bottom right
                // stays where it was.
                Left = br.X - Width;
                Top = br.Y - Height;

                // ...but make sure the title bar is still on screen
                Rectangle scr = Screen.FromControl(this).Bounds;
                if (Top < scr.Top)
                    Top = scr.Top;
                if (Left < scr.Left)
                    Left = scr.Left;
            }

            // make sure we redraw the interior at the new rotation
            Invalidate();
        }

        private void btnDone_Click(object sender, EventArgs e)
        {
            Setup(false);
        }

        private void OSDWin_Move(object sender, EventArgs e)
        {
            SavePos();
        }

        void SavePos()
        {
            if (setupMode)
            {
                Point pos = PointToScreen(new Point(0, 0));
                mainwin.cfg.OSDPos = new Rectangle(pos.X, pos.Y, ClientSize.Width, ClientSize.Height);
                mainwin.SetCfgDirty();
            }
        }


        private Point GetCenter(Rectangle r)
        {
            float x = (r.Width / 2.0f) + r.X;
            float y = (r.Height / 2.0f) + r.Y;
            return new Point((int)Math.Round(x), (int)Math.Round(y));
        }

        private PointF GetCenter(RectangleF r)
        {
            float x = (r.Width / 2.0f) + r.X;
            float y = (r.Height / 2.0f) + r.Y;
            return new PointF(x, y);
        }

        private enum AlignMode { Top, Bottom, Left, Right, HCenter, VCenter, LeftOf, RightOf, Above, Below };


        // move r2 to align with r1 based on the mode, and adjusted by the offset
        // LeftOf, RightOf, Above, and Below treat offset as a marging value regardless of direction.
        // (a positive value distances r2 from r1 by the given positive distance. A negative value would cause the rects to overlap.)
        private void AlignRect(Rectangle r1, ref Rectangle r2, AlignMode mode, int offset = 0)
        {
            switch (mode)
            {
                case AlignMode.Top: { r2.Y = r1.Y + offset; break; }
                case AlignMode.Bottom: { r2.Y = (r1.Bottom - r2.Height) + offset; break; }
                case AlignMode.Left: { r2.X = r1.X + offset; break; }
                case AlignMode.Right: { r2.X = (r1.Right - r2.Width) + offset; break; }
                case AlignMode.HCenter: { r2.X = (int) Math.Round(GetCenter(r1).X - (r2.Width / 2.0f)) + offset; break; }
                case AlignMode.VCenter: { r2.Y = (int) Math.Round(GetCenter(r1).Y - (r2.Height / 2.0f)) + offset; break; }
                case AlignMode.LeftOf: { r2.X = r1.Left - r2.Width - offset; break; }
                case AlignMode.RightOf: { r2.X = r1.Right + offset; break; }
                case AlignMode.Above: { r2.Y = r1.Top - r2.Height - offset; break; }
                case AlignMode.Below: { r2.Y = r1.Bottom + offset; break; }
                default: { break; }
            }
        }

        private bool IsLockVisible(UIWin.VolumeMode mode, OSDType osd)
        {
            switch (osd)
            {
                case OSDType.Global:
                    {
                        return (mode == UIWin.VolumeMode.Night && mainwin.cfg.NightVolLock);
                    }
                default: { return false; }
            }
        }

        //creates a path for a rounded rectangle
        private GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            Size size = new Size(diameter, diameter);
            Rectangle arc = new Rectangle(bounds.Location, size);
            GraphicsPath path = new GraphicsPath();

            if (radius == 0)
            {
                path.AddRectangle(bounds);
                return path;
            }

            // top left arc  
            path.AddArc(arc, 180, 90);

            // top right arc  
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);

            // bottom right arc  
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);

            // bottom left arc 
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);

            path.CloseFigure();
            return path;
        }

        //draws a rounded rectangle to the provided graphics object
        private void DrawRoundedRectangle(Graphics gr, Pen pen, Rectangle bounds, int cornerRadius)
        {
            if (gr == null)
                throw new ArgumentNullException(nameof(gr));
            if (pen == null)
                throw new ArgumentNullException(nameof(pen));

            using (GraphicsPath path = RoundedRect(bounds, cornerRadius))
            {
                gr.DrawPath(pen, path);
            }
        }

        //draws a filled rounded rectangle to the provided graphics object
        private void FillRoundedRectangle(Graphics gr, Brush brush, Rectangle bounds, int cornerRadius)
        {
            if (gr == null)
                throw new ArgumentNullException(nameof(gr));
            if (brush == null)
                throw new ArgumentNullException(nameof(brush));

            using (GraphicsPath path = RoundedRect(bounds, cornerRadius))
            {
                gr.FillPath(brush, path);
            }
        }

        private void DrawLockIcon(Graphics gr, RectangleF iconRect)
        {
            SmoothingMode origSmooth = gr.SmoothingMode;
            gr.SmoothingMode = SmoothingMode.HighQuality;

            float radiusScale = 0.20f;
            const int borderPx = 2;
            Color shadowClr = Color.FromArgb(255, 0, 0, 0);
            //Color borderClr = Color.FromArgb(255, 100, 0, 0);
            Color borderClr = Color.FromArgb(50, 0, 0);
            Color bgClr = Color.FromArgb(255, 200, 0, 0);

            Pen p = new Pen(borderClr, borderPx);
            SolidBrush b = new SolidBrush(shadowClr);

            //RectangleF shadowRect = iconRect;
            //shadowRect.Offset(3, 2);
            //shadowRect.Inflate(2, 2);

            //FillRoundedRectangle(gr, b, Rectangle.Round(shadowRect), (int)(Math.Min(shadowRect.Width, shadowRect.Height) * radiusScale));
            b.Color = bgClr;
            Brush bgbrush = new LinearGradientBrush(iconRect, Color.FromArgb(255, 170, 170), Color.FromArgb(100, 0, 0), 60.0f);
            FillRoundedRectangle(gr, bgbrush, Rectangle.Round(iconRect), (int)(Math.Min(iconRect.Width, iconRect.Height) * radiusScale));
            DrawRoundedRectangle(gr, p, Rectangle.Round(iconRect), (int)(Math.Min(iconRect.Width, iconRect.Height) * radiusScale));

            RectangleF imgRect = iconRect;
            imgRect.Inflate(-0.1f * iconRect.Width, -0.1f * iconRect.Height);
            gr.DrawImage(lockedVol, imgRect);

            gr.SmoothingMode = origSmooth;
        }


    }
}
