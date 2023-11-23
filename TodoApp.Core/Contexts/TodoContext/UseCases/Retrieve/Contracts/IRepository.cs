using TodoApp.Core.Contexts.TodoContext.Entities;

namespace TodoApp.Core.Contexts.TodoContext.UseCases.Retrieve.Contracts;
public interface IRepository
{
    Task<Todo?> GetTodoById(Guid id);

    Task<List<Todo>> GetTodosAsync(Guid? id = null);
}