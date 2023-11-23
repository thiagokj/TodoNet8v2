# Todo NET8 v2 - Minimal API de lista de tarefas

Esse projeto é a continuidade do projeto [v1](https://github.com/thiagokj/TodoNet8v1)

Vamos implementar as funcionalidades de leitura, alteração e exclusão de tarefas.

## CORE

### CRIANDO MÉTODOS PARA MANIPULAÇÃO DE DADOS

1. Como estamos trabalhando com UseCases, crie um caso de uso para recuperar as tarefas (Retrieve).

```csharp
// Primeiro crie a interface para definir os métodos de consulta aos dados no repositório externo
namespace TodoApp.Core.Contexts.TodoContext.UseCases.Retrieve.Contracts;
public interface IRepository
{
    // Retorna a tarefa conforme o ID informado
    Task<Todo?> GetTodoById(Guid id);

    // Retorna todas as tarefas, com a flexibilidade de filtrar ou não por Id
    Task<List<Todo>> GetTodosAsync(Guid? id = null);
}
```

## INFRA

### IMPLEMENTE OS MÉTODOS PARA USO DO REPOSITÓRIO

```csharp
using Microsoft.EntityFrameworkCore;
using TodoApp.Core.Contexts.TodoContext.Entities;
using TodoApp.Core.Contexts.TodoContext.UseCases.Retrieve.Contracts;
using TodoApp.Infra.Data;

namespace TodoApp.Infra.Contexts.TodoContext.UseCases.Retrieve;

public class Repository(AppDbContext context) : IRepository
{
    private readonly AppDbContext _context = context;

    public async Task<Todo?> GetTodoById(Guid id) =>
        await _context
            .Todos
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

    public async Task<List<Todo>> GetTodosAsync(Guid? id = null)
    {
        IQueryable<Todo> query = _context.Todos.AsNoTracking();

        if (id.HasValue)
            query = query.Where(x => x.Id == id);

        return await query.ToListAsync();
    }
}
```

## CORE

### FLUXO DE PROCESSO PARA RECUPERAR AS TAREFAS

1. Crie o request, passando o Id da tarefa

```csharp
using MediatR;

namespace TodoApp.Core.Contexts.TodoContext.UseCases.Retrieve;

public record Request(Guid Id) : IRequest<Response>;
```

1. Crie o response, para retorno de uma ou mais tarefas

```csharp
using Flunt.Notifications;

namespace TodoApp.Core.Contexts.TodoContext.UseCases.Retrieve;
public class Response : SharedContext.UseCases.Response
{
    protected Response()
    {
    }

    public Response(
    string message,
    int status,
    IEnumerable<Notification>? notifications = null)
    {
        Message = message;
        Status = status;
        Notifications = notifications;
    }

    public Response(string message, ResponseData data)
    {
        Message = message;
        Status = 200;
        Notifications = null;
        Data = data;
    }
    public Response(string message, List<ResponseData> data)
    {
        Message = message;
        Status = 200;
        Notifications = null;
        DataList = data;
    }

    public ResponseData? Data { get; set; }

    public List<ResponseData>? DataList { get; set; }
}

public record ResponseData(Guid Id, string Title, bool IsComplete);
```

1. Não será criada uma especificação, então vamos partir para o Handler.

```csharp
using MediatR;
using TodoApp.Core.Contexts.TodoContext.Entities;
using TodoApp.Core.Contexts.TodoContext.UseCases.Retrieve.Contracts;

namespace TodoApp.Core.Contexts.TodoContext.UseCases.Retrieve;
public class Handler : IRequestHandler<Request, Response>
{
    private readonly IRepository _repository;

    public Handler(IRepository repository) => _repository = repository;

    public async Task<Response> Handle(Request request, CancellationToken cancellationToken)
    {
        #region 01. Recupera a tarefa

        Todo? todo;
        try
        {
            todo = await _repository.GetTodoById(request.Id);
            if (todo is null)
                return new Response("Tarefa não encontrada", 404);
        }
        catch (Exception)
        {
            return new Response("Não foi possível recuperar a tarefa", 500);
        }

        #endregion

        return new Response(
            "Tarefa encontrada",
            new ResponseData(todo.Id, todo.Title, todo.IsComplete));
    }
}
```

1. Uma outra abordagem para trazer apenas 1 item pelo Id ou trazer uma lista é alterar o Handler dessa forma.

```csharp
...
public class Handler : IRequestHandler<Request, Response>
{
    ...
    public async Task<Response> Handle(Request request, CancellationToken cancellationToken)
    {
        List<Todo> todos;
        try
        {
            if (request.Id != Guid.Empty)
            {
                // Se o ID for fornecido, use o método GetTodoById para obter uma única tarefa
                var todo = await _repository.GetTodoById(request.Id);
                if (todo == null)
                    return new Response("Tarefa não encontrada", 404);

                var responseData = new ResponseData(todo.Id, todo.Title, todo.IsComplete);
                return new Response("Tarefa recuperada", responseData);
            }

            // Se o ID não foi fornecido ou é Guid.Empty, use o método GetTodosAsync para obter todas as tarefas
            todos = await _repository.GetTodosAsync();
        }
        catch (Exception)
        {
            return new Response("Não foi possível recuperar as tarefas", 500);
        }

        var responseDataList = todos
            .Select(todo =>
                new ResponseData(todo.Id, todo.Title, todo.IsComplete))
            .ToList();

        return new Response("Todas as tarefas recuperadas", responseDataList);
    }
}
```

## API

### INJETAR REPOSITÓRIO E ENDPOINTS

1. Agora adicione os novos métodos ao TodoContextExtension.

```csharp
public static class TodoContextExtension
{
    public static void AddTodoContext(this WebApplicationBuilder builder)
    {
        // Método Create...

        #region Retrieve

        builder.Services.AddTransient<
            TodoApp.Core.Contexts.TodoContext.UseCases.Retrieve.Contracts.IRepository,
            TodoApp.Infra.Contexts.TodoContext.UseCases.Retrieve.Repository>();

        #endregion
    }

    public static void MapTodoEndpoints(this WebApplication app)
    {
        // Rota do método Create...

        #region Retrieve

        // Retorna a tarefa pelo Id informado. Se o Id não for informado,
        // envia o id vazio e retorna uma lista com todas as tarefas
        app.MapGet("api/v1/todos/{id?}", async (
            Guid? id,
            IRequestHandler<
                TodoApp.Core.Contexts.TodoContext.UseCases.Retrieve.Request,
                TodoApp.Core.Contexts.TodoContext.UseCases.Retrieve.Response> handler) =>
        {
            var requestId = id ?? Guid.Empty;
            var request = new TodoApp.Core.Contexts.TodoContext.UseCases.Retrieve.Request(requestId);

            var result = await handler.Handle(request, new CancellationToken());
            return result.IsSuccess
                ? Results.Ok(result)
                : Results.Json(result, statusCode: result.Status);
        });

        #endregion
    }
```
