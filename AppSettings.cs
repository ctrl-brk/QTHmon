// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable UnusedMember.Global
namespace QTHmon
{
    public class QthKeywordSearch
    {
        public string Keywords { get; set; }
        public int MaxPages { get; set; }
        public string ResultFile { get; set; }
    }

    public class QthCategorySearch
    {
        public string Categories { get; set; }
        public int MaxPages { get; set; }
        public string ResultFile { get; set; }
    }

    public class SwapQthCom
    {
        public string Title { get; set; }
        public QthKeywordSearch KeywordSearch { get; set; }
        public QthCategorySearch CategorySearch { get; set; }
    }

    public class AppSettings
    {
        public string SmtpServer { get; set; }
        public int Port { get; set; }
        public string User { get; set; }
        public string Password { get; set; }
        public string EmailFrom { get; set; }
        public string EmailTo { get; set; }
        public string EmailSubjectFormat { get; set; }
        public SwapQthCom SwapQthCom { get; set; }
    }
}
