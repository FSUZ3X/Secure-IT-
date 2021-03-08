using System.IO;

namespace SSP 
{
    class Logger // логировать все действия программы, ошибки, обнаружения флешек
    {
        internal static void LogEvent(string msg)  // этот метод универсально логгирует все события в удобной для чтения форме
        {
            try
            {
                if (Form1.start_param != @"-Temp" || Form1.NeedToLogFromTemp)
                {

                    if (!File.Exists(Setup.Shelter + "Logfile.log"))
                    {
                        File.Create(Setup.Shelter + "Logfile.log").Close();
                    }

                    if ((System.DateTime.Now - File.GetCreationTime(Setup.Shelter + "Logfile.log")).TotalDays > 60)
                    {
                        File.Delete(Setup.Shelter + "Logfile.log");
                        File.Create(Setup.Shelter + "Logfile.log").Close();
                    }

                    msg += " (" + (System.DateTime.Now).ToString("dd.MM.yyyy|HH:mm:ss") + ")";

                    string CorrectMsg = "";
                    int startindex = 0;      //первая буква реквизита, от которого начинаем считать до следующего пробела
                    int endindex = 0;        //пробел, до которого считаем

                    for (int i = 0; i < msg.Length; i++)
                    {
                        if (msg[i].ToString() == " ")
                        {
                            endindex = i;

                            for (int a = 0; a < 34 - (endindex - startindex); a++)
                            {
                                CorrectMsg += " ";
                            }
                            startindex = i;
                        }
                        else { CorrectMsg = CorrectMsg + msg[i].ToString(); }
                    }


                    StreamWriter streamwriter = new StreamWriter(Setup.Shelter + "Logfile.log", true, System.Text.Encoding.GetEncoding("utf-8"));
                    streamwriter.WriteLine(CorrectMsg);
                    streamwriter.Close();
                }
            }
            catch { }
        }
    }
}
