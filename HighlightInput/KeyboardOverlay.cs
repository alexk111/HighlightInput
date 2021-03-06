﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HighlightInput
{
    class KeyboardOverlay : IDisposable
    {
        #region Tweakables
        readonly double c_appearMillis = TimeSpan.FromSeconds(0.1f).TotalMilliseconds;
        readonly double c_fadeMillis = TimeSpan.FromSeconds(1).TotalMilliseconds;
        readonly double c_fadeStartDelay = TimeSpan.FromSeconds(1).TotalMilliseconds;

        readonly float c_percentFromBottom = 0.2f;
        readonly int c_paddingX = 20;
        readonly int c_paddingY = 10;
        readonly int c_border = 6;

        readonly Dictionary<Keys, string> c_keyReplacements = new Dictionary<Keys, string>()
        {
            { Keys.LWin, "Win" },
            { Keys.Oem1, ";" },
            { Keys.Oem5, "\\" },
            { Keys.Oem6, "]" },
            { Keys.Oem7, "'" },
            { Keys.Oem8, "`" },
            { Keys.Oemtilde, "`" },
            { Keys.Oemcomma, "," },
            { Keys.OemPeriod, "." },
            { Keys.OemQuestion, "/" },
            { Keys.OemOpenBrackets, "[" },
            { Keys.OemMinus, "Минус" },
            { Keys.Oemplus, "=" },
            { Keys.Add, "Плюс" },
            { Keys.Subtract, "Минус" },
            { Keys.Multiply, "*" },
            { Keys.Divide, "/" },
            { Keys.D0, "0" }, { Keys.D1, "1" }, { Keys.D2, "2" }, { Keys.D3, "3" }, { Keys.D4, "4" }, { Keys.D5, "5" }, { Keys.D6, "6" }, { Keys.D7, "7" }, { Keys.D8, "8" }, { Keys.D9, "9" },
            { Keys.NumPad0, "0" }, { Keys.NumPad1, "1" }, { Keys.NumPad2, "2" }, { Keys.NumPad3, "3" }, { Keys.NumPad4, "4" }, { Keys.NumPad5, "5" }, { Keys.NumPad6, "6" }, { Keys.NumPad7, "7" }, { Keys.NumPad8, "8" }, { Keys.NumPad9, "9" },
            { Keys.Back, "Backspace" },
            { Keys.Decimal, "." },
            { Keys.Space, "Пробел" },
            { Keys.Return, "Enter" },
            { Keys.PageUp, "Page Up" },
            { Keys.Next, "Page Down" },
            { Keys.Escape, "Esc" },
            { Keys.Up, "Стрелка Вверх" }, { Keys.Down, "Стрелка Вниз" }, { Keys.Left, "Стрелка Влево" }, { Keys.Right, "Стрелка Вправо" },
        };
        #endregion

        #region State
        private object m_lock = new object();
        private GameOverlay.Windows.GraphicsWindow m_overlay;
        private GameOverlay.Windows.WindowBounds m_desktopBounds;

        private DateTime m_shownTime = DateTime.MinValue;
        private bool m_keyReleased = true;
        private string m_text;
        #endregion

        public KeyboardOverlay()
        {
            GameOverlay.Drawing.Rectangle rect = GetDesktopRect();
            m_overlay = new GameOverlay.Windows.GraphicsWindow((int)rect.Left, (int)rect.Top, (int)(rect.Right - rect.Left), (int)(rect.Bottom - rect.Top),
                new GameOverlay.Drawing.Graphics() { PerPrimitiveAntiAliasing = true, TextAntiAliasing = true });

            m_overlay.DrawGraphics += DrawGraphics;
            m_overlay.FPS = 60;
            m_overlay.Create();

            m_overlay.IsTopmost = true;
            m_overlay.Show();
        }

        //TODO: re-check desktop size occasionally & call m_overlay.Resize with this rect
        private GameOverlay.Drawing.Rectangle GetDesktopRect()
        {
            lock (m_lock)
            {
                var desktop = GameOverlay.Windows.WindowHelper.GetDesktopWindow();

                if (desktop == IntPtr.Zero)
                {
                    throw new Exception("Unable to get the desktop window");
                }

                if (!GameOverlay.Windows.WindowHelper.GetWindowBounds(desktop, out m_desktopBounds))
                {
                    throw new Exception("Unable to get the desktop bounds");
                }

                int middleX = m_desktopBounds.Right / 2;
                int middleY = (int)(m_desktopBounds.Bottom * (1.0f - c_percentFromBottom));

                //define a max size for the overlay, but what the user sees will be much smaller (measures the text)
                int maxWidth = 1000;
                int maxHeight = 200;

                if ((m_desktopBounds.Bottom - middleY) < maxHeight)
                {
                    middleY = m_desktopBounds.Bottom - maxHeight;
                }

                return GameOverlay.Drawing.Rectangle.Create(middleX - maxWidth / 2, middleY, maxWidth, maxHeight);
            }
        }

        public void KeyDown(KeyEventArgs args)
        {
            lock(m_lock)
            {
                //exit shortcut: Ctrl Alt Escape
                if(args.Control && args.Alt && args.KeyCode == Keys.Escape)
                {
                    args.Handled = true;
                    Application.Exit();
                }

                string str = args.KeyCode.ToString();

                //do we have a nice alias?
                if(c_keyReplacements.ContainsKey(args.KeyCode))
                {
                    str = c_keyReplacements[args.KeyCode];
                }

                //don't show any modifiers on their own
                //TODO: stop combinations of modifiers showing without actual keys
                if ((args.Modifiers == Keys.None || args.Control || args.Alt || args.Shift) && (str.Contains("Shift") || str.Contains("Control") || str.Contains("Menu")))
                {
                    if (args.Modifiers == Keys.None)
                    {
                        if (str.Contains("Shift")) {
                            str = "Shift";
                        }
                        if (str.Contains("Control")) {
                            str = "Control";
                        }
                        if (str.Contains("Menu")) {
                            str = "Alt";
                        }
                        m_text = str;
                    }
                    else
                    {
                        m_text = $"{args.Modifiers}";
                    }
                }
                else
                {
                    m_text = args.Modifiers == 0 ? str : $"{args.Modifiers} + {str}";
                }

                if (m_text.Contains("Control")) {
                    m_text = m_text.Replace("Control", "Ctrl");
                }
                m_keyReleased = false;
                m_text = m_text.ToUpper();
                m_shownTime = DateTime.Now;
            }
        }

        public void KeyUp(KeyEventArgs args)
        {
            lock(m_lock)
            {
                if (m_keyReleased)
                {
                    return;
                }
                m_keyReleased = true;
                m_shownTime = DateTime.Now;
            }
        }

        //TODO: hook setup & destroy graphics properly
        private void DrawGraphics(object sender, GameOverlay.Windows.DrawGraphicsEventArgs e)
        {
            lock (m_lock)
            {
                e.Graphics.ClearScene();

                if (!string.IsNullOrEmpty(m_text))
                {
                    double millisElapsed = m_keyReleased ? DateTime.Now.Subtract(m_shownTime).TotalMilliseconds : 0;

                    float sizeMultip = 1.0f;
                    if (millisElapsed < c_appearMillis) {
                      sizeMultip = 1.0f + (float)((1 - millisElapsed/c_appearMillis) * 0.2f);
                    }

                    GameOverlay.Drawing.Font font = e.Graphics.CreateFont("Calibri", 50 * sizeMultip, true);
                    GameOverlay.Drawing.Point textSize = e.Graphics.MeasureString(font, m_text);

                    int middleX = m_overlay.Width / 2;
                    int middleY = m_overlay.Height / 2;

                    if (millisElapsed > c_fadeStartDelay + c_fadeMillis)
                    {
                        return;
                    }

                    float alpha = 1.0f;
                    if(millisElapsed > c_fadeStartDelay)
                    {
                        alpha = 1.0f - (float)((millisElapsed - c_fadeStartDelay) / c_fadeMillis);
                    }

                    var backColour = e.Graphics.CreateSolidBrush(0.6f, 0.6f, 0.6f, 0.85f * alpha);
                    var fontColour = e.Graphics.CreateSolidBrush(0f, 0f, 0f, alpha);

                    var rect = GameOverlay.Drawing.Rectangle.Create((int)(middleX - textSize.X / 2), (int)(middleY - textSize.Y / 2), (int)textSize.X, (int)textSize.Y);
                    Inflate(ref rect, (int)(c_paddingX*sizeMultip), (int)(c_paddingY*sizeMultip));

                    e.Graphics.FillRectangle(backColour, rect);
                    if(c_border > 0)
                    {
                        Inflate(ref rect, c_border / 2, c_border / 2);
                        e.Graphics.DrawRectangle(fontColour, rect, c_border);
                    }

                    e.Graphics.DrawText(font, fontColour, middleX - textSize.X / 2, middleY - textSize.Y / 2, m_text);
                }
            }
        }

        public void Dispose()
        {
            lock (m_lock)
            {
                m_overlay.Dispose();
            }
        }

        private void Inflate(ref GameOverlay.Drawing.Rectangle rect, int inflateX, int inflateY)
        {
            rect.Left -= inflateX;
            rect.Top -= inflateY;
            rect.Right += inflateX;
            rect.Bottom += inflateY;
        }
    }
}
