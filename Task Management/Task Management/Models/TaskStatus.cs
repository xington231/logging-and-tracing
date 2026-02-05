namespace Task_Management.Models
{
    public class StatusTask
    {
        public int IdTaskStatus { get; set; }
        public string StatusName { get; set; } 
        public ICollection<CurrentTask> CurrentTasks { get; set; }
    }
}
