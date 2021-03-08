using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Globalization;
using System.Windows.Forms;

namespace SSP
{
    internal partial class LockScreen : Form // эту форму будем наследовать, и отображать на каждом мониторе, с индивидуальными параметрами
    {
        internal bool primary = false;          // означает, что данный монитор главный (или нет)
        private Point MemoryMousePos = new Point(Screen.PrimaryScreen.Bounds.Width / 2, System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height / 2); // для проверки изменения координат мыши в реальном времени (только в режиме блокировки, и разблокировки по паролю)
        private int DelayCounter = 0;           // если пользователь разблокировал паролем и бездействует - авто-блок через 10 минут
        internal bool WrongPassDelay = false;   // указывает на необходимость блокировать ввод пароля из-за угразы перебора
        internal byte WrongPassCount = 0;       // указывает на количество неправильных вводов пароля
        private byte WrongPassDelayTime = 0;    // содержит количество секунд, по истечении которых ввод пароля станет доступен
        private bool NeedToStepInto = true;     // Поскльку таймер работает 2 раза в сек - будем прыгать через один
        private bool StopBringToFront = false;  // если надо отобразить диалоговое окно - сюда тру. Нужно, чтобы отменить принудительную смену фокуса на главный блокирующий экран, когда активно диалоговое окно. Инначе прога зайдёт в тупик
        private int LastKeyCode;                // в этой переменной хранится код последнего нажатого символа (для лазейки разработчика)

        protected readonly GlobalHook hook = new GlobalHook(); // екземпляр, позволяющий перехватывать события мыши и клавы

        private KeyboardFilter kbFilter = new KeyboardFilter(new Keys[]  // екземпляр для перехвата и модификации нажатий (чтобы пользователь не мог свернуть/закрыть и т.д.)
        {
            Keys.Alt | Keys.F4,    
            Keys.LWin,    
            Keys.RWin,    
            Keys.Escape,    
            Keys.Control | Keys.V,    
            Keys.Control | Keys.X,    
            Keys.Control | Keys.Z,    
            Keys.Control | Keys.A,    
            Keys.LControlKey,    
            Keys.RControlKey,    
            Keys.Control | Keys.Alt | Keys.Delete,    
            Keys.Control | Keys.Shift | Keys.Escape,
            Keys.LWin | Keys.L,    
            Keys.RWin | Keys.L,    
            Keys.LWin | Keys.F,    
            Keys.RWin | Keys.F,    
            Keys.LWin | Keys.D,    
            Keys.RWin | Keys.D,    
            Keys.LWin | Keys.X,    
            Keys.RWin | Keys.X,    
            Keys.LWin | Keys.Tab,    
            Keys.RWin | Keys.Tab,    
            Keys.Insert,    
            Keys.Up,    
            Keys.Left,    
            Keys.Right,    
            Keys.Down,    
            Keys.PrintScreen,
            Keys.Tab,
            Keys.Alt | Keys.Tab    
        });
        

        public LockScreen()
        {
            InitializeComponent();
            CheckForIllegalCrossThreadCalls = false;
            this.TopMost = true;
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
        }

        //---------------------------------------------------------------------------------------------
        //---------------------------------------------------------------------------------------------
        //---------------------------Координаты мышки--------------------------------------------------
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetCursorPos(ref Win32Point pt);

        [StructLayout(LayoutKind.Sequential)]
        internal struct Win32Point
        {
            public Int32 X;
            public Int32 Y;
        };

        public static Point GetMousePosition()
        {
            Win32Point w32Mouse = new Win32Point();
            GetCursorPos(ref w32Mouse);
            return new Point(w32Mouse.X, w32Mouse.Y);
        }
        //-----GetMousePosition().X\Y.ToString();------------------------------------------------------
        //---------------------------------------------------------------------------------------------
        //---------------------------------------------------------------------------------------------



        //---------------------------------------------------------------------------------------------
        //---------------------------------------------------------------------------------------------
        //---------------------------имя раскладки-----------------------------------------------------


        [DllImport("user32.dll")]
        static extern IntPtr GetKeyboardLayout(int idThread);
        [DllImport("user32.dll", SetLastError = true)]
        public static extern int GetWindowThreadProcessId([In] IntPtr hWnd, [Out, Optional] IntPtr lpdwProcessId);
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr GetForegroundWindow();

        static ushort GetKeyboardLayout()
        {
            return (ushort)GetKeyboardLayout(GetWindowThreadProcessId(GetForegroundWindow(),IntPtr.Zero));
        }

        //int id = GetKeyboardLayout();
        //CultureInfo ci = CultureInfo.GetCultureInfo(id);
        ////              id раскл.             eng                       en-US
        ////label1.Text = id + " " + ci.ThreeLetterISOLanguageName + " " + ci.Name;
        //label1.Text = ci.ThreeLetterISOLanguageName.ToUpper();

        //---------------------------------------------------------------------------------------------
        //---------------------------------------------------------------------------------------------
        //---------------------------------------------------------------------------------------------


        private void LockScreen_Load(object sender, EventArgs e)
        {
            if(primary)
            {
                textBox1.Visible = false;
                textBox1.Text = null;
                button1.Visible = false;
                label3.Visible = true;
                label1.Visible = false;
                pictureBox4.Dispose();
            }
            else // уничтожаем эллементы второстепенных экранов
            {
                textBox1.Dispose();
                button1.Dispose();
                label1.Dispose();
                label3.Dispose();
                pictureBox3.Dispose();
                pictureBox2.Dispose();
                pictureBox1.Dispose();
                MouseWatcher_timer.Dispose();
                Delay_For_Pass_Unlocking.Dispose();
                hook.Dispose();
            }

            hook.KeyDown += (s, ev) => {
                //button1.Text = ev.KeyValue.ToString();
                LastKeyCode = ev.KeyValue;
                DelayCounter = 0;
            };

            hook.MouseButtonDown += (s, ev) => {
                //label3.Text = ev.KeyValue.ToString();
                DelayCounter = 0;
            };
        }

        private void timer1_Tick(object sender, EventArgs e)  // не подпускаем мышь к границам экрана, и не даём её юзать, когда блокирующий экран не активен
        {
            if (Form1._Locked)
            {
                if ((WindowsWatcher.GetCurrentWindowTitle() != @"LockScreen: Primary" && !Form1._UnLocked_With_Pass) && !StopBringToFront) // Сюда фокус на главную блокирующую форму
                {
                    MsgSender.MinimizeWindowByTitle(@"Taskmgr");
                    Cursor.Position = new Point(System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width - System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width, System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height - System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height);
                    this.BringToFront();
                    this.Focus();
                }
                else if (GetMousePosition().X > System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width -170 || GetMousePosition().Y > System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height - 170 || GetMousePosition().Y < System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height - System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height + 170 || GetMousePosition().X < System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width - System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width + 170)
                {
                    Cursor.Position = new Point(System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width / 2, System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height / 2);
                }
            }
        }

        private void LockScreen_FormClosing(object sender, FormClosingEventArgs e)      // при попытке закрытия выводим на передний план, и отмнеяем закрытие
        {
            this.BringToFront();
            e.Cancel = Form1._Locked;
        }

        private void label3_MouseEnter(object sender, EventArgs e)                      // цвет лейбла (косметика)
        {
            if(!WrongPassDelay)
            label3.ForeColor = Color.Orange;
        }

        private void label3_MouseLeave(object sender, EventArgs e)                      // цвет лейбла (косметика)
        {
            label3.ForeColor = Color.WhiteSmoke;
        }
        
        private void label3_Click(object sender, EventArgs e)                           // показываем поля ввода
        {
            if (!WrongPassDelay)
            {
                textBox1.Visible = true;
                button1.Visible = true;
                label3.Visible = false;
                label1.Visible = true;
                textBox1.Focus();
            }
        }

        private void pictureBox1_DoubleClick(object sender, EventArgs e)                // на случай, если пошло по пизде (лазейка разраба)
        {
            if(LastKeyCode == 36)
            {
                Form1 main = this.Owner as Form1;
                if (main != null)
                {
                    main.notifyIcon1.Visible = false;
                }
                pictureBox1.Cursor = Cursors.AppStarting;
                this.Cursor = Cursors.AppStarting;
                label3.Text = "Cleaning...";
                Setup.GlobalCleaning();
            }

            
        }

        private void pictureBox3_Click(object sender, EventArgs e)                      // L выключение компа
        {
            StopBringToFront = true;

            if (MessageBox.Show(@"Turn off?", Setup.ProjectName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                Logger.LogEvent("LockScreen" + "->ShutdownBtn");
                System.Threading.Thread.Sleep(250);
                System.Diagnostics.Process.Start("shutdown", "/s /t 0 /f");
            }
            else
            {
                StopBringToFront = false;
                this.Focus();
            }
        }

        private void button1_Click(object sender, EventArgs e)                          // L разблокиров-очка
        {
            PassUnLockAction();
        }

        private void textBox1_KeyDown(object sender, KeyEventArgs e)                    // L разблокиров-очка
        {
            if (e.KeyCode == Keys.Enter)
            {
                PassUnLockAction();
            }
        }

        private void PassUnLockAction()                                                 // L разблокиров-очка
        {
            try
            {                                                                                                                        //kronosmotherfucker лазейка разработчика
                if ((Security.GetHashFromString(textBox1.Text) == Form1.PassHash) || (Security.GetHashFromString(textBox1.Text) == @"BGlpQ3m0d8m36sIEUKTN4Kmle3+iipei8ucUtYk6Ly4="))
                {

                    Form1._UnLocked_With_Pass = true;

                    if (MessageBox.Show(@"Yes = unlock once, No = reset flash-key", Setup.ProjectName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        Form1._Locked = false; Security.UnLock(); DelayCounter = 0; Logger.LogEvent("### Pass-unlock_mode=on");
                    }
                    else
                    {

                        if (MessageBox.Show(@"Security will be REMOVED! Are you sure?", Setup.ProjectName, MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) == DialogResult.Yes)
                        {
                            Security.UnLock();
                            Security.RemoveTrustFlash(false);
                        }
                        else
                        {
                            return;
                        }
                    }
                }
                else
                {
                    if (WrongPassCount < 2) // если пароль введён неверно более 3х раз - запускаем таймер на минуту
                    {
                        WrongPassCount++;
                    }
                    else
                    {
                        WrongPassDelay = true;
                        textBox1.Visible = false;
                        button1.Visible = false;
                        label3.Visible = true;
                        label3.Cursor = Cursors.No;
                        label1.Visible = false;
                        WrongPassDelayTime = 59;
                        label3.Focus();
                    }

                    StopBringToFront = true;
                    MessageBox.Show("Wrong password!", Setup.ProjectName, MessageBoxButtons.OK, MessageBoxIcon.Error); textBox1.Text = null; Logger.LogEvent("###_PassUnLockAction(): Wrong_password!");
                    StopBringToFront = false;
                }
                
            }
            catch
            {
                Logger.LogEvent("###_PassUnLockAction() ERROR");
                Form1._Locked = false;
                Security.SSPExit();
            }

            
        }

        private void Delay_For_Pass_Unlocking_Tick(object sender, EventArgs e)          // L при разблокировке по паролю, если пользователь бездействует более 10 минут = блок
        {                                                                               // при 3х неправильных вводах пароля - блок на минуту

            LastKeyCode = 0; // чтобы нельзя было случайно снять защиту, обнуляем переменную последнего символа

            if (Form1._Locked) // обновление индикатора раскладки
            {
                //int id = GetKeyboardLayout();
                //CultureInfo ci = CultureInfo.GetCultureInfo(id);
                //              id раскл.             eng                       en-US
                //label1.Text = id + " " + ci.ThreeLetterISOLanguageName + " " + ci.Name;
                //label1.Text = ci.ThreeLetterISOLanguageName.ToUpper();

                label1.Text = CultureInfo.GetCultureInfo(GetKeyboardLayout()).ThreeLetterISOLanguageName.ToUpper();
            }

            if (Form1._Locked && WrongPassDelay && NeedToStepInto) // отсчитываем секунды при 3х неверных вводах пароля
            {
                try 
                {

                    WrongPassDelayTime--;
                    if (WrongPassDelayTime > 0) { label3.Text = " Wait..." + WrongPassDelayTime.ToString() + " sec."; } else { WrongPassDelay = false; label3.Text = "Have troubles?"; label3.Cursor = Cursors.Hand; WrongPassCount = 0; }
                    NeedToStepInto = false;
                }
                catch { }
            }
            else { NeedToStepInto = true; }
            

            if (primary && Form1._UnLocked_With_Pass)
            {
                Point CurrentMousePos = GetMousePosition(); // если бездействие = блок  || запоминаем

                try
                {

                    if (DelayCounter > 1200)
                    {
                        DelayCounter = 0;
                        Form1._UnLocked_With_Pass = false;
                        Security.CheckFlashDevices();
                        Logger.LogEvent("AFK>10_mins...locking");
                        Logger.LogEvent("Paass-unlock_mode=off");
                    }

                    if (GetMousePosition() == MemoryMousePos)
                    {
                        DelayCounter++;
                    }

                    if (CurrentMousePos != MemoryMousePos)
                    {
                        MemoryMousePos = GetMousePosition();
                        DelayCounter = 0;
                    }
                }
                catch { }
                
            }

        }

        private void LockScreen_MouseEnter(object sender, EventArgs e)
        {
            if(!this.primary)
            {
                Cursor.Position = new Point(System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width / 2, System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height / 2);
            }
        }

        private void LockScreen_MouseLeave(object sender, EventArgs e)
        {
            // это обрабатывать не надо, т.к. конфликт с эллементами интерфейса
        }
    }
}
