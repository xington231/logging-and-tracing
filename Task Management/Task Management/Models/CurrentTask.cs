namespace Task_Management.Models
{
    public class CurrentTask
    {
        public int task_id { get; set; }
        public string task_name { get; set; }
        public string task_description { get; set; }
        public DateOnly dateadded { get; set; }
        public DateOnly deadlinedate { get; set; }
        public bool iscompleted { get; set; }
        public int statusid { get; set; }
        public int priorityid { get; set; }
    }
}
