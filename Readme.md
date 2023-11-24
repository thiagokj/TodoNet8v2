# Todo NET8 v2 - Minimal API de lista de tarefas

Esse projeto é a continuidade do projeto [v1](https://github.com/thiagokj/TodoNet8v1)

Vamos implementar as funcionalidades de leitura, alteração e exclusão de tarefas.

Passos:

1. CORE - Criar uma interface IRepository e definir as assinaturas dos métodos de acesso.
2. INFRA - Criar um repositório e implementar os métodos de acesso.
3. CORE - Criar o fluxo de processo com Request, Response, Specification e Handler para manipular os dados.
4. API - Injetar o repositório e definir os endpoints com as rotas do Handler.

## CORE - RETRIEVE

### CRIANDO MÉTODOS PARA MANIPULAÇÃO DE DADOS

1. Como estamos trabalhando com UseCases, crie um caso de uso para recuperar as tarefas (Retrieve).

```csharp
// Primeiro crie a interface para definir os métodos de consulta aos dados no repositório externo
using TodoApp.Core.Contexts.TodoContext.Entities;

namespace TodoApp.Core.Contexts.TodoContext.UseCases.Retrieve.Contracts;

public interface IRepository
{
    Task<Todo?> GetByIdAsync(Guid id);

    Task<List<Todo>> GetAllAsync(Guid? id = null);
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

    public async Task<Todo?> GetByIdAsync(Guid id) =>
        await _context
            .Todos
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

    public async Task<List<Todo>> GetAllAsync(Guid? id = null)
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
            todo = await _repository.GetByIdAsync(request.Id);
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
                var todo = await _repository.GetById(request.Id);
                if (todo == null)
                    return new Response("Tarefa não encontrada", 404);

                var responseData = new ResponseData(todo.Id, todo.Title, todo.IsComplete);
                return new Response("Tarefa recuperada", responseData);
            }

            // Se o ID não foi fornecido ou é Guid.Empty, use o método GetTodosAsync para obter todas as tarefas
            todos = await _repository.GetAllAsync();
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

## CORE - UPDATE

1. Vamos tornar os métodos set públicos, apenas para baixar a complexidade do código. Em futuras versões, serão feitas melhorias nessa questão.

```csharp
...
// Classe Todo...
    public Todo(string title, bool isComplete)
    {
        Title = title;
        IsComplete = isComplete;
    }

    public string Title { get; set; } = string.Empty;
    public bool IsComplete { get; set; } = false;
```

### CRIANDO MÉTODOS PARA ATUALIZAÇÃO DE DADOS

1. Como estamos trabalhando com UseCases, crie um caso de uso para atualizar as tarefas (Update).

```csharp
// Primeiro crie a interface para definir os métodos de atualização dos dados no repositório externo
using TodoApp.Core.Contexts.TodoContext.Entities;
namespace TodoApp.Core.Contexts.TodoContext.UseCases.Update.Contracts;

public interface IRepository
{
    // Recupera o todo para posterior atualização
    Task<Todo?> GetByIdAsync(Guid id);
    Task UpdateAsync(Todo todo);
}
```

## INFRA

### IMPLEMENTE OS MÉTODOS PARA USO DO REPOSITÓRIO

```csharp
using Microsoft.EntityFrameworkCore;
using TodoApp.Core.Contexts.TodoContext.Entities;
using TodoApp.Core.Contexts.TodoContext.UseCases.Update.Contracts;
using TodoApp.Infra.Data;

namespace TodoApp.Infra.Contexts.TodoContext.UseCases.Update;
public class Repository(AppDbContext context) : IRepository
{
    private readonly AppDbContext _context = context;

    // Método auxiliar para recuperar um todo
    public async Task<Todo?> GetByIdAsync(Guid id) =>
        await _context.Todos.FirstOrDefaultAsync(t => t.Id == id);

    public async Task UpdateAsync(Todo todo)
    {
        var existingTodo = await
                _context
                .Todos
                .FirstOrDefaultAsync(t => t.Id == todo.Id);

        // Verifica se existe o todo e atualiza suas propriedades
        if (existingTodo != null)
        {
            existingTodo.Title = todo.Title;
            existingTodo.IsComplete = todo.IsComplete;

            await _context.SaveChangesAsync();
        }
    }
}
```

## CORE

### FLUXO DE PROCESSO PARA ATUALIZAR AS TAREFAS

1. Crie o request, passando o Id da tarefa e suas propriedades

```csharp
using MediatR;

namespace TodoApp.Core.Contexts.TodoContext.UseCases.Update;

public record Request(Guid Id, string Title, bool IsComplete) : IRequest<Response>;
```

1. Agora crie o Response, para retornar a tarefa atualizada

```csharp
using Flunt.Notifications;

namespace TodoApp.Core.Contexts.TodoContext.UseCases.Update;
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

    public ResponseData? Data { get; set; }
}

public record ResponseData(Guid Id, string Title, bool IsComplete);
```

1. Defina as especificações para validação ao atualizar a tarefa

```csharp
using Flunt.Notifications;
using Flunt.Validations;

namespace TodoApp.Core.Contexts.TodoContext.UseCases.Update;
public static class Specification
{
    public static Contract<Notification> Ensure(Request request)
        => new Contract<Notification>()
            .Requires()
            .IsNotNullOrWhiteSpace(request.Id.ToString(), "Id", "Id não informado")
            .IsLowerThan(
                request.Title.Length,
                160,
                "Title",
                "A tarefa deve conter menos que 160 caracteres")
            .IsGreaterThan(
                request.Title.Length,
                3,
                "Title",
                "A tarefa deve conter mais que 3 caracteres");
}
```

1. Por fim, crie o Handler para executar o fluxo de atualização da tarefa

```csharp
using MediatR;
using TodoApp.Core.Contexts.TodoContext.Entities;
using TodoApp.Core.Contexts.TodoContext.UseCases.Update.Contracts;

namespace TodoApp.Core.Contexts.TodoContext.UseCases.Update;
public class Handler : IRequestHandler<Request, Response>
{
    private readonly IRepository _repository;

    public Handler(IRepository repository) => _repository = repository;

    public async Task<Response> Handle(Request request, CancellationToken cancellationToken)
    {
        #region 01. Valida a requisição

        try
        {
            var res = Specification.Ensure(request);
            if (!res.IsValid)
                return new Response("Requisição inválida", 400, res.Notifications);
        }
        catch
        {
            return new Response("Não foi possível validar sua requisição", 500);
        }

        #endregion

        #region 02. Recupera a tarefa

        Todo? todo;

        try
        {
            if (request.Id == Guid.Empty)
                return new Response("Id não informado", 404);

            todo = await _repository.GetByIdAsync(request.Id);
            if (todo == null)
                return new Response("Tarefa não encontrada", 404);

            todo.Title = request.Title;
            todo.IsComplete = request.IsComplete;
        }
        catch (Exception ex)
        {
            return new Response(ex.Message, 400);
        }

        #endregion

        #region 03. Persiste os dados

        try
        {
            await _repository.UpdateAsync(todo);
        }
        catch
        {
            return new Response("Falha ao persistir dados", 500);
        }

        #endregion

        return new Response(
            "Tarefa atualizada",
            new ResponseData(todo.Id, todo.Title, todo.IsComplete));
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
        // Método Retrieve...

        #region Update

        builder.Services.AddTransient<
            TodoApp.Core.Contexts.TodoContext.UseCases.Update.Contracts.IRepository,
            TodoApp.Infra.Contexts.TodoContext.UseCases.Update.Repository>();

        #endregion

    }

    public static void MapTodoEndpoints(this WebApplication app)
    {
        // Rota do método Retrieve...

        #region Update

        app.MapPut("api/v1/todos", async (
            TodoApp.Core.Contexts.TodoContext.UseCases.Update.Request request,
            IRequestHandler<
                TodoApp.Core.Contexts.TodoContext.UseCases.Update.Request,
                TodoApp.Core.Contexts.TodoContext.UseCases.Update.Response> handler) =>
        {
            var result = await handler.Handle(request, new CancellationToken());
            return result.IsSuccess
              ? Results.Ok(result)
              : Results.Json(result, statusCode: result.Status);
        });

        #endregion
    }
```

## CORE - DELETE

### CRIANDO MÉTODOS PARA EXCLUSÃO DE DADOS

1. Para finalizar, vamos configurar a aplicação para apagar uma tarefa, criando uma interface com os métodos.

```csharp
namespace TodoApp.Core.Contexts.TodoContext.UseCases.Delete.Contracts;
public interface IRepository
{
    Task DeleteAsync(Guid id);
}
```

## INFRA

### IMPLEMENTE OS MÉTODOS PARA USO DO REPOSITÓRIO

1. Agora precisamos definir o repositório que faz a exclusão da tarefa.

```csharp
using TodoApp.Core.Contexts.TodoContext.UseCases.Delete.Contracts;
using TodoApp.Infra.Data;

namespace TodoApp.Infra.Contexts.TodoContext.UseCases.Delete;
public class Repository(AppDbContext context) : IRepository
{
    private readonly AppDbContext _context = context;

    public async Task DeleteAsync(Guid id)
    {
        var todo = await _context.Todos.FindAsync(id);
        if (todo != null)
        {
            _context.Todos.Remove(todo);
            await _context.SaveChangesAsync();
        }
    }
}
```

## CORE

### FLUXO DE PROCESSO PARA APAGAR AS TAREFAS

1. Agora os comandos para excluir as tarefas

```csharp
// Request
using MediatR;

namespace TodoApp.Core.Contexts.TodoContext.UseCases.Delete;
public record Request(Guid Id) : IRequest<Response>;

// Response
using Flunt.Notifications;

namespace TodoApp.Core.Contexts.TodoContext.UseCases.Delete;
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
        Status = 204;
        Notifications = null;
        Data = data;
    }

    public ResponseData? Data { get; set; }
}

public record ResponseData(Guid Id);
```

1. O Handler para executar os comandos

```csharp
using MediatR;
using TodoApp.Core.Contexts.TodoContext.UseCases.Delete.Contracts;

namespace TodoApp.Core.Contexts.TodoContext.UseCases.Delete;
public class Handler : IRequestHandler<Request, Response>
{
    private readonly IRepository _repository;

    public Handler(IRepository repository) => _repository = repository;

    public async Task<Response> Handle(Request request, CancellationToken cancellationToken)
    {

        #region 01. Exclui a tarefa

        try
        {
            if (request.Id == Guid.Empty)
                return new Response("Id não informado", 404);

            await _repository.DeleteAsync(request.Id);
        }
        catch (Exception ex)
        {
            return new Response(ex.Message, 400);
        }

        #endregion

        return new Response(
            "Tarefa excluída",
            new ResponseData(request.Id));
    }
}
```

## API

### INJETAR REPOSITÓRIO E ENDPOINTS

1. Atualize a API, injetando o repositório para apagar dados.

```csharp
    // Demais métodos...

     #region Delete

     builder.Services.AddTransient<
         TodoApp.Core.Contexts.TodoContext.UseCases.Delete.Contracts.IRepository,
         TodoApp.Infra.Contexts.TodoContext.UseCases.Delete.Repository>();

     #endregion
```

1. Crie a rota para apagar a tarefa informando o Id

```csharp
    // Demais rotas acima...

     #region Delete

     app.MapDelete("api/v1/todos/{id}", async (
         Guid id,
         IRequestHandler<
             TodoApp.Core.Contexts.TodoContext.UseCases.Delete.Request,
             TodoApp.Core.Contexts.TodoContext.UseCases.Delete.Response> handler) =>
     {
         var request = new TodoApp.Core.Contexts.TodoContext.UseCases.Delete.Request(id);
         var result = await handler.Handle(request, new CancellationToken());
         return result.IsSuccess
             ? Results.NoContent()
             : Results.Json(result, statusCode: result.Status);
     });

     #endregion
```
