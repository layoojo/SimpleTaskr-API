public class TasksItemDTO
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public bool IsComplete { get; set; }
    public string? Details { get; set; }
    public int AssignedToID { get; set; } 

    public TasksItemDTO() { }
    public TasksItemDTO(SimpleTaskr task) =>
    (Id, Name, IsComplete, Details, AssignedToID) = (task.Id, task.Name, task.IsComplete, task.Details, task.AssignedToID);
}