using System;
using System.Diagnostics;

namespace QTHmon
{
    [DebuggerDisplay("Id = {Id}, Title = {Title}")]
    public class Post
    {
        public int Id { get; set; }
        public string Category { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public bool HasImage { get; set; }
        public DateTime SubmittedOn { get; set; }
        public DateTime? ModifiedOn { get; set; }
        public string CallSign { get; set; }
        public string Price { get; set; }
        public bool IsNew { get; set; }

        public DateTime ActivityDate => ModifiedOn ?? SubmittedOn;
    }
}
