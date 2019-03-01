namespace QTHmon
{
    public class AppSettings
    {
        public string SmtpServer { get; set; }
        public int Port { get; set; }
        public string User { get; set; }
        public string Password { get; set; }
        public string EmailFrom { get; set; }
        public string EmailTo { get; set; }
        public string EmailSubjectFormat { get; set; }
        public string Keywords { get; set; }
        public int MaxPages { get; set; }
        public string ResultFile { get; set; }
    }
}
