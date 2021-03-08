using System;
using System.Collections;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace SSP
{
    internal partial class Form1 : Form
    {
        internal static bool needtorestart=false;
        internal static bool IsSecured;                                                // указывает на то, защищена ли система в данный момент (наличие ключа в реестре)
        internal static bool _Locked;                                                  // флаг с текущим состоянием
        internal static Screen[] All_Screens = Screen.AllScreens;                      // содержит инфу о мониторах
        internal static LockScreen[] Lock_Forms = new LockScreen[All_Screens.Length];  // индивидуальные формы для блокировки каждого монитора, основываясь на его инфе
        internal static bool _UnLocked_With_Pass = false;                              // указывает на то, включена ли разблокировка паролем в данный момент
        internal static string start_param = null;                                     // тут хранится параметр командной строки
        internal static bool NeedToLogFromTemp = false;                                // по умолчанию логгирование из Temp отключено, но если таки нужно - сюда тру
        internal static string FlashHash = null;                                       // сюда поместим хеши флешки и пароля, чтобы не дёргать реестр при каждом втыке флешки
        internal static string PassHash = null;                                        //


        //--------------------------------для того, чтобы таскать прогу за любое место---------------------------------------------------
        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam); 
        [System.Runtime.InteropServices.DllImportAttribute("user32.dll")]
        public static extern bool ReleaseCapture();                                     
        //-------------------------------------------------------------------------------------------------------------------------------



        //-----------------------------------------------------------------------------------------------------------------------
        //-----------------------------------ОБНАРУЖЕНИЕ ФЛЕШКИ В РЕАЛЬНОМ ВРЕМЕНИ-----------------------------------------------
        //-----------------------------------------------------------------------------------------------------------------------

        private const int WM_DEVICECHANGE = 0x0219;
        private const int DBT_DEVICEREMOVECOMPLETE = 0x8004;
        private const int DBT_DEVICEARRIVAL = 0x8000;
        private const uint DBT_DEVTYP_VOLUME = 0x00000002;

        [StructLayout(LayoutKind.Sequential)]
        private struct DEV_BROADCAST_HDR
        {
            public uint dbch_size;
            public uint dbch_devicetype;
            public uint dbch_reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DEV_BROADCAST_VOLUME
        {
            public uint dbcv_size;
            public uint dbcv_devicetype;
            public uint dbcv_reserved;
            public uint dbcv_unitmask;
            public ushort dbcv_flags;
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == WM_DEVICECHANGE)
            {
                switch (m.WParam.ToInt32())
                {
                    case DBT_DEVICEARRIVAL:
                        DEV_BROADCAST_HDR dbhARRIVAL = (DEV_BROADCAST_HDR)Marshal.PtrToStructure(m.LParam, typeof(DEV_BROADCAST_HDR));
                        if (dbhARRIVAL.dbch_devicetype == DBT_DEVTYP_VOLUME)
                        {
                            DEV_BROADCAST_VOLUME dbv = (DEV_BROADCAST_VOLUME)Marshal.PtrToStructure(m.LParam, typeof(DEV_BROADCAST_VOLUME));
                            BitArray bArray = new BitArray(new byte[]
                            {
                                (byte)(dbv.dbcv_unitmask & 0x00FF),
                                (byte)((dbv.dbcv_unitmask & 0xFF00) >> 8),
                                (byte)((dbv.dbcv_unitmask & 0xFF0000) >> 16),
                                (byte)((dbv.dbcv_unitmask & 0xFF000000) >> 24)
                            });

                            int DriveLetter = Char.ConvertToUtf32("A", 0);
                            for (int i = 0; i < bArray.Length; i++)
                            {
                                if (bArray.Get(i))  // Флешка определена...
                                {

                                    if (IsSecured) // защита установлена
                                    {
                                        new Thread(() =>
                                        {
                                            Security.CheckFlashDevices();
                                        }).Start();
                                    }
                                    else { StartFormInterface(); }
                                    DriveLetter += 1;
                                }
                            }
                        }
                        break;
                    case DBT_DEVICEREMOVECOMPLETE:
                        DEV_BROADCAST_HDR dbhREMOVECOMPLETE = (DEV_BROADCAST_HDR)Marshal.PtrToStructure(m.LParam, typeof(DEV_BROADCAST_HDR));
                        if (dbhREMOVECOMPLETE.dbch_devicetype == DBT_DEVTYP_VOLUME)
                        {
                            DEV_BROADCAST_VOLUME dbv = (DEV_BROADCAST_VOLUME)Marshal.PtrToStructure(m.LParam, typeof(DEV_BROADCAST_VOLUME));
                            BitArray bArray = new BitArray(new byte[]
                            {
                                (byte)(dbv.dbcv_unitmask & 0x00FF),
                                (byte)((dbv.dbcv_unitmask & 0xFF00) >> 8),
                                (byte)((dbv.dbcv_unitmask & 0xFF0000) >> 16),
                                (byte)((dbv.dbcv_unitmask & 0xFF000000) >> 24)
                            });

                            int driveLetter = Char.ConvertToUtf32("A", 0);
                            for (int i = 0; i < bArray.Length; i++)
                            {
                                if (bArray.Get(i)) // ФЛЕШКА ПОТЕРЯНА, ТОГДА...
                                {
                                    if (!IsSecured)
                                    {
                                        StartFormInterface();
                                    }
                                    else
                                    {
                                        if (!_UnLocked_With_Pass)
                                        {
                                            new Thread(() =>
                                            {
                                                Security.CheckFlashDevices();
                                            }).Start();
                                        }
                                    }
                                }
                                driveLetter += 1;
                            }
                        }
                        break;
                }
            }
        }

        //-----------------------------------------------------------------------------------------------------------------------
        //-----------------------------------------------------------------------------------------------------------------------
        //-----------------------------------------------------------------------------------------------------------------------

        // действия, до запуска формы
        public Form1(string[] args)                                                      
        {
            Thread.Sleep(500);                  // чтобы процедура установки отработала нормально, и AntiDouble() не сработал при перезапуске

            IsSecured = Security.Is_Secured();  // проверяем защищена ли система
            if (start_param == "-Temp" && !IsSecured)
            {
                Security.SSPExit();
            }

            if (args.Length > 0)
            {
                start_param = args[0];
            }

            // убиваем себя, если экземпляр уже запущен и имя изменено
            if (IsSecured && Security.Is_Runned() && Path.GetFileNameWithoutExtension(Application.ExecutablePath) != Setup.ProjectName)
            {
                if (start_param == @"-Shortcut")
                {
                    MessageBox.Show("System Secured! Use lockscreen to reset flash-key!", Setup.ProjectName);
                }
                Security.SSPExit();
            }

            Security.AntiDouble();              // убиваем себя, если экземпляр уже запущен

            if (IsSecured && (start_param == "-Temp"))
            {
                NeedToLogFromTemp = true;
                Logger.LogEvent("### System_Restored!");
            }

            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)                 // проверяем защищена ли система
        {
            this.Text = Setup.ProjectName;

            // открыть окно добавления флешки
            if (!IsSecured) 
            {
                notifyIcon1.Visible = false;
                this.ShowIcon = true;
                CheckForIllegalCrossThreadCalls = false;    // чтобы можно было вызывать эллементы управления не созданные в этом потоке, из разных потоков
                progressBar1.Value = progressBar1.Minimum;

                StartFormInterface();                       // отображаем стартовый интерфейс
            }

            // проверить носители
            else
            {
                Logger.LogEvent("System_Started!");

                FlashHash = Security.GetFlashHashFromReg();
                PassHash = Security.GetPassHashFromReg(); 

                this.ShowIcon = false;
                Setup.SetupSSP();
                this.Hide();                 // прячем форму
                this.ShowInTaskbar = false;  // скрываем иконку

                BuildLockScreens(); // собираем экраны блокировки

                new Thread(() =>
                {
                    Security.CheckFlashDevices();
                }).Start();

                notifyIcon1.Visible = true;
            }

            // ПРИМЕР ССЫЛКИ
            //                 ref                       ComboBox             ComboWithDrives        =                 ref                                comboBox1;
            // ключевое слово ссылки на объект   |||   тип объекта   |||   имя переменной-ссылки   |||    ключевое слово ссылки на объект   |||    имя объекта, на который указывает ссылка
            //Security.TakeDrives(ComboWithDrives); // Передаём ссылку в виде параметра, потом обращаемся к нему как обычно
        }

        internal void BuildLockScreens()                                    // заполняем массив форм  
        {
            int counter = 0;
            foreach (Screen s in All_Screens)
            {

                if (s != null)
                {
                    if (s.Primary) // если экран главный - отобразить интерфейс
                    {
                        LockScreen ls = new LockScreen();

                        ls.Owner = this;

                        //------------------показываем интерфейс
                        ls.primary = true;
                        ls.Text = "LockScreen: Primary";
                        ls.textBox1.Visible = false;                                                                   
                        ls.button1.Visible = true;                                                                       
                        ls.label3.Visible = true;                                                                        
                        ls.pictureBox3.Visible = true;

                        //---------------подганяем форму под размеры экрана и в центр
                        ls.Location = new Point(Convert.ToInt32(s.WorkingArea.Left), Convert.ToInt32(s.WorkingArea.Top)); 
                        ls.Size = new Size(s.WorkingArea.Width, s.WorkingArea.Height);                                    
                        ls.WindowState = FormWindowState.Maximized;

                        //---------------Размер Картинок
                        if (ls.Size.Height < ls.Size.Width)
                        {
                            ls.pictureBox1.Size = new Size(ls.Size.Height / 6, ls.Size.Height / 6);     //6                                                   //| подганяем logo под размеры экрана
                            ls.pictureBox2.Size = new Size(ls.pictureBox1.Size.Height + ls.pictureBox1.Size.Height * 1/3, ls.pictureBox1.Size.Height * 3); //| подганяем название под размеры экрана
                        }
                        else
                        {
                            ls.pictureBox1.Size = new Size(ls.Size.Width / 6, ls.Size.Width / 6);                                                         //| подганяем logo под размеры экрана
                            ls.pictureBox2.Size = new Size(ls.pictureBox1.Size.Width + ls.pictureBox1.Size.Height * 1/3, ls.pictureBox1.Size.Height * 3); //| подганяем название под размеры экрана
                        }

                        //---------------Положение Картинок
                        ls.pictureBox4.Visible = false;
                        ls.pictureBox1.Location = new Point((ls.Size.Width / 2 - (ls.pictureBox1.Size.Width + ls.pictureBox2.Size.Width) / 2 - ls.label3.Size.Width / 2), ls.Size.Height / 2 - ls.pictureBox1.Size.Height / 2); //| пропорциональный перенос лого в центр, с учетом размеров надписи
                        ls.pictureBox2.Location = new Point((ls.pictureBox1.Location.X + ls.pictureBox1.Size.Width), ls.Size.Height / 2 - ls.pictureBox2.Size.Height / 2);                           //| левый край надписи к правому краю лого

                        //-----------------Пложение Эллементов управления                                                                  
                        ls.label3.Location = new Point(ls.Size.Width / 2 - ls.label3.Size.Width / 2, ls.Size.Height / 2 + ls.pictureBox1.Size.Height / 2 + 5);                                                    //|Have troubles?
                        ls.textBox1.Location = new Point(ls.label3.Location.X - (ls.textBox1.Size.Width + ls.button1.Size.Width)/3, ls.label3.Location.Y);                             //|Поле для пароля
                        ls.button1.Location = new Point(ls.textBox1.Location.X + ls.textBox1.Size.Width, ls.textBox1.Location.Y);                                                      //|Кнопка для сброса пароля
                        ls.pictureBox3.Location = new Point(ls.label3.Location.X + ls.label3.Size.Width/4, ls.textBox1.Location.Y + ls.pictureBox3.Size.Height);                       //|Кнопка выключения пк 
                        ls.label1.Location = new Point(ls.Size.Width - 90, ls.Size.Height - 20);

                        Lock_Forms[counter] = ls; // Сохраняем в массив
                    }
                    else // это для всех остальных экранов
                    {
                        LockScreen ls = new LockScreen();

                        ls.Owner = this;

                        //---------------прячем интерфейс                                                                     
                        ls.textBox1.Visible = false;                                                                 
                        ls.button1.Visible = false;                                                                       
                        ls.pictureBox2.Visible = false;
                        ls.pictureBox1.Visible = true;
                        ls.label3.Visible = false;
                        ls.label1.Visible = false;
                        ls.pictureBox3.Visible = false;

                        //-----------------подганяем форму под размеры экрана и в центр
                        ls.Location = new Point(Convert.ToInt32(s.WorkingArea.Left), Convert.ToInt32(s.WorkingArea.Top));  
                        ls.Size = new Size(s.WorkingArea.Width, s.WorkingArea.Height);                                     
                        ls.WindowState = FormWindowState.Maximized;

                        //--------------лого в центр
                        ls.pictureBox4.Visible = true;
                        ls.pictureBox1.Visible = false;
                        ls.pictureBox4.Size = new Size(ls.Size.Width / 2, ls.Size.Height / 2);                     
                        ls.pictureBox4.Location = new Point(((ls.Size.Width / 2 - ls.pictureBox4.Size.Width / 2)), ls.Size.Height / 2 - ls.pictureBox4.Size.Height / 2);

                        Lock_Forms[counter] = ls;
                    }
                    counter++;
                }
            } // создаём экраны блокировки для каждого монитора
        }

        internal void StartFormInterface()   // отображает стартовую форму, и весь интерфейс на ней, все флешки в comboBox1 (далее можно обращаться к ним по drive.VolumeLabel)
        {
            comboBox1.Items.Clear();
            comboBox1.Text = "";
            this.Show();
            this.ShowInTaskbar = true;
            ShowTimer.Enabled = true;

            foreach (DriveInfo drive in Security.TakeDrives()) //запрашиваем массив флешек
            {
                if (drive != null)
                {
                    if((drive.VolumeLabel+drive.Name).Length == 3)
                    {
                        comboBox1.Items.Add(drive.Name);
                        comboBox1.Text = drive.Name;
                    }
                    else
                    {
                        comboBox1.Items.Add(drive.VolumeLabel + " " + drive.Name);//кладём флешки в список
                        comboBox1.Text = drive.VolumeLabel + " " + drive.Name;
                    } 
                }
            }

            if (comboBox1.Items.Count < 1)
            {
                comboBox1.Text = "";
                comboBox1.Text = "USB Drives not found";
                textBox1.ReadOnly = true;
                textBox2.ReadOnly = true;
                textBox1.Enabled = false;
                textBox2.Enabled = false;

                textBox1.Text = "";
                textBox2.Text = "";
            }
            else
            {
                textBox1.ReadOnly = false;
                textBox2.ReadOnly = false;
                textBox1.Enabled = true;
                textBox2.Enabled = true;
            }
        }

        private void ShowTimer_Tick(object sender, EventArgs e)             // плавное появление при запуске (одноразовое)
        {
            if ((Opacity += 0.024d) == 1) ShowTimer.Dispose();
        }

        private void CheckStartFormInterface()                              // проверка введённых данных, регистрация флешки, установка
        {
            if (!checkBox1.Checked)
            {
                MessageBox.Show("You should accept author`s policy!", Setup.ProjectName);
                label5.ForeColor = Color.Orange;
                label5.Focus();
            }
            else if (comboBox1.Items.Count < 1 || comboBox1.Text == "USB Drives not found")
            {
                MessageBox.Show("Please, connect your flash Drive!", Setup.ProjectName);
            }
            else if (textBox1.Text.Length < progressBar1.Maximum)
            {
                MessageBox.Show("This password too short!", Setup.ProjectName);
                textBox1.Focus();
            }
            else if (textBox1.Text != textBox2.Text)
            {
                MessageBox.Show("Passwords do not match!", Setup.ProjectName);
                textBox1.Text = "";
                textBox2.Text = "";
                textBox1.Focus();
            }
            
            else
            {
                string letter = null;

                if(comboBox1.Text.Length == 3)
                {
                    letter = comboBox1.Text;
                }
                else
                {
                    letter = comboBox1.Text.Substring(comboBox1.Text.Length-3, 3);
                }

                notifyIcon1.Visible = false;
                //this.Hide();

                button1.Text = "Installing...";

                // затемняем интерфейс
                comboBox1.ForeColor = Color.Gray;
                textBox1.Enabled = false;
                textBox2.Enabled = false;
                button1.ForeColor = Color.Gray;
                checkBox1.ForeColor = Color.Gray;
                label1.ForeColor = Color.Gray;
                label5.ForeColor = Color.Gray;
                label2.ForeColor = Color.Gray;
                label3.ForeColor = Color.Gray;
                label4.ForeColor = Color.Gray;

                // крутим колёсико
                this.Cursor = Cursors.WaitCursor;
                panel1.Cursor = Cursors.WaitCursor;
                pictureBox1.Cursor = Cursors.WaitCursor;
                label1.Cursor = Cursors.WaitCursor;
                label2.Cursor = Cursors.WaitCursor;
                label3.Cursor = Cursors.WaitCursor;
                label4.Cursor = Cursors.WaitCursor;
                label5.Cursor = Cursors.WaitCursor;
                checkBox1.Cursor = Cursors.WaitCursor;
                comboBox1.Cursor = Cursors.WaitCursor;
                textBox1.Cursor = Cursors.WaitCursor;
                textBox2.Cursor = Cursors.WaitCursor;
                button1.Cursor = Cursors.WaitCursor;

                label1.Focus();

                //MessageBox.Show("");
                Security.AddTrustFlash(letter, textBox1.Text); // делаем ключики
                needtorestart = true;
                this.Hide();
                Setup.SetupSSP();                              // копируемся, прописываемся в автостарт (если уже уст - это не выполнится)
            }
        }

        private void notifyIcon1_Click(object sender, EventArgs e)          // блокировка любой кнопкой мыши по иконке в трее
        {
            _UnLocked_With_Pass = false;
            Logger.LogEvent("notifyIcon->checking...");
            Security.CheckFlashDevices();
        }

        private void ProgressBarsDisplay()          // визуализация проверки пароля
        {
            if ((textBox1.Text.Length >= progressBar1.Maximum) && (textBox2.Text == textBox1.Text))
            {
                progressBar2.Value = progressBar1.Maximum;
            }
            else { progressBar2.Value = 0; }
        }

        //КОСМЕТИКА---------КОСМЕТИКА--------КОСМЕТИКА---------КОСМЕТИКА------КОСМЕТИКА-----КОСМЕТИКА--------КОСМЕТИКА--------КОСМЕТИКА-------------------
        //КОСМЕТИКА---------КОСМЕТИКА--------КОСМЕТИКА---------КОСМЕТИКА------КОСМЕТИКА-----КОСМЕТИКА--------КОСМЕТИКА--------КОСМЕТИКА-------------------
        //КОСМЕТИКА---------КОСМЕТИКА--------КОСМЕТИКА---------КОСМЕТИКА------КОСМЕТИКА-----КОСМЕТИКА--------КОСМЕТИКА--------КОСМЕТИКА-------------------


        private void comboBox1_Click(object sender, EventArgs e)            // клик на comboBox1
        {
            comboBox1.DroppedDown = true; // выбрасываем список
            comboBox1.SelectionLength = 0;// убираем выделение
        }

        private void comboBox1_KeyPress(object sender, KeyPressEventArgs e) // чтобы нельзя было писать в comboBox1
        {
            e.KeyChar = '\0';
        }

        private void button1_Click(object sender, EventArgs e)              // Когда юзверь ввёл инфу
        {
            CheckStartFormInterface();
            checkBox1.Focus();
        }

        private void textBox1_TextChanged(object sender, EventArgs e)       // прогресс напротив пароля
        {
            if (textBox1.Text.Length < progressBar1.Maximum)
            {
                progressBar1.Value = textBox1.Text.Length;
            }
            else { progressBar1.Value = progressBar1.Maximum; }

            ProgressBarsDisplay();
        }

        private void Form1_MouseDown(object sender, MouseEventArgs e)       //для того, чтобы таскать за любое место
        {
            if (e.Button == (MouseButtons.Left))
            {
                ReleaseCapture();
                SendMessage(this.Handle, 0xA1, 0x2, 0); 
            }
        }

        private void label1_MouseDown(object sender, MouseEventArgs e)      //для того, чтобы таскать за любое место
        {
            if (e.Button == (MouseButtons.Left))
            {
                ReleaseCapture();
                SendMessage(this.Handle, 0xA1, 0x2, 0); 
            }
        }

        private void label2_MouseDown(object sender, MouseEventArgs e)      //для того, чтобы таскать за любое место
        {
            if (e.Button == (MouseButtons.Left))
            {
                ReleaseCapture();
                SendMessage(this.Handle, 0xA1, 0x2, 0); 
            }
        }

        private void label3_MouseDown(object sender, MouseEventArgs e)      //для того, чтобы таскать за любое место
        {
            if (e.Button == (MouseButtons.Left))
            {
                ReleaseCapture();
                SendMessage(this.Handle, 0xA1, 0x2, 0); 
            }
        }

        private void label4_MouseDown(object sender, MouseEventArgs e)      //для того, чтобы таскать за любое место
        {
            if (e.Button == (MouseButtons.Left))
            {
                ReleaseCapture();
                SendMessage(this.Handle, 0xA1, 0x2, 0); 
            }
        }

        private void pictureBox1_MouseDown(object sender, MouseEventArgs e) //для того, чтобы таскать за любое место
        {
            if (e.Button == (MouseButtons.Left))
            {
                ReleaseCapture();
                SendMessage(this.Handle, 0xA1, 0x2, 0);
            }
        }

        private void panel1_MouseDown(object sender, MouseEventArgs e)      //для того, чтобы таскать за любое место
        {
            if (e.Button == (MouseButtons.Left))
            {
                ReleaseCapture();
                SendMessage(this.Handle, 0xA1, 0x2, 0);
            }
        }

        private void textBox1_KeyDown(object sender, KeyEventArgs e)        // смена фокуса
        { 
            if (e.KeyCode == Keys.Enter)
            {
                textBox2.Focus();
            }
        }   

        private void textBox2_KeyDown(object sender, KeyEventArgs e)        // смена фокуса
        {
            if (e.KeyCode == Keys.Enter)
            {
                CheckStartFormInterface();
            }
        }

        private void label1_MouseEnter(object sender, EventArgs e)          // подсветка автора
        {
            label1.ForeColor = Color.Orange;
        }

        private void label1_MouseLeave(object sender, EventArgs e)          // подсветка автора
        {
            label1.ForeColor = Color.WhiteSmoke;
        }

        private void label5_MouseEnter(object sender, EventArgs e)          // подсветка соглашения
        {
            label5.ForeColor = Color.Orange;
        }

        private void label5_MouseLeave(object sender, EventArgs e)          // подсветка соглашения
        {
            label5.ForeColor = Color.WhiteSmoke;
        }

        private void label5_Click(object sender, EventArgs e)               // отображение соглашения, принял
        {
            //Original
            //MessageBox.Show("       You know, what I think about agreements like this? \n\n                            FUCK THE AGREEMENTS!", Setup.ProjectName);
            //MessageBox.Show("To be serious, this license covers author`s intellectual property.\nBy accepting this agreement, you disclaim author`s liability\nfor any damage caused to your information, hardware or any\ntechnical means.", Setup.ProjectName);
            //label5.Text = "";
            //label1.Text = "";
            //checkBox1.Text = "Yeah, fuck the agreements!";

            //main
            MessageBox.Show("This license covers author`s intellectual property.\nBy accepting this agreement, you disclaim author`s\nliability for any damage caused to your information,\nhardware or any technical means.", Setup.ProjectName);
        }

        private void label1_Click(object sender, EventArgs e)               // клик по автору
        {
            MessageBox.Show("github.com/FSUZ3X/");
        }

        private void pictureBox2_Click(object sender, EventArgs e)          // выход
        {
            Application.Exit();
        }

        private void comboBox1_SelectedValueChanged(object sender, EventArgs e) // чёртово выделение текста в comboBox1
        {
            label1.Focus();
        }

        private void comboBox1_MouseMove(object sender, MouseEventArgs e)       // чёртово выделение текста в comboBox1
        {
            comboBox1.SelectionLength = 0;
        }

        private void Form1_MouseMove(object sender, MouseEventArgs e)           // чёртово выделение текста в comboBox1
        {
            comboBox1.SelectionLength = 0;
        }

        private void checkBox1_Click(object sender, EventArgs e)                // чёртово выделение текста в checkBox1
        {
            label5.Focus();
        }

        private void textBox2_TextChanged(object sender, EventArgs e)
        {
            ProgressBarsDisplay();
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            label5.ForeColor = Color.Orange;
        }
    }
    
}
