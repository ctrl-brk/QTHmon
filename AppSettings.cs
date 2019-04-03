// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable UnusedMember.Global
namespace QTHmon
{
    public class KeywordSearch
    {
        public string Keywords { get; set; }
        public int MaxPosts { get; set; }
        public string ResultFile { get; set; }
    }

    public class CategorySearch
    {
        public string Categories { get; set; }
        public int MaxPosts { get; set; }
        public string ResultFile { get; set; }
    }

    public class Cache
    { 
        /// <summary>
        /// Will be combined with ResourceUrl
        /// </summary>
        /// <value>Where to cache images</value>
        public string ImageFolder { get; set; }
    }

    public class QthCom
    {
        public string Title { get; set; }
        public KeywordSearch KeywordSearch { get; set; }
        public CategorySearch CategorySearch { get; set; }
        public Cache Cache { get; set; }
    }

    public class EhamNet
    {
        public string Title { get; set; }
        public KeywordSearch KeywordSearch { get; set; }
        public CategorySearch CategorySearch { get; set; }
        public Cache Cache { get; set; }
    }

    public class AppSettings
    {
        public string SmtpServer { get; set; }
        public int Port { get; set; }
        public string User { get; set; }
        public string Password { get; set; }
        public string EmailFrom { get; set; }
        public string EmailTo { get; set; }
        public string EmailSubjectResultsFormat { get; set; }
        public string EmailSubjectEmptyFormat { get; set; }
        /// <summary>
        /// File name to save generated html for an email/external access
        /// </summary>
        public string BodyFileName { get; set; }
        /// <summary>
        /// Whether or not to attach email body also as an html file
        /// </summary>
        public bool AttachFile { get; set; }
        /// <summary>
        /// Url to access cached assets via http
        /// </summary>
        public string ResourceUrl { get; set; }
        public QthCom QthCom { get; set; }
        public EhamNet EhamNet { get; set; }
    }
}
