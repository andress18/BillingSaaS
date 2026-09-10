using BillingSaaS.Application.Common.Models;

namespace BillingSaaS.Application.TodoLists.Queries.GetTodos;

public class TodosVm
{
    public IReadOnlyCollection<LookupDto> PriorityLevels { get; init; } = [];

    public IReadOnlyCollection<ColourDto> Colours { get; init; } = [];

    public IReadOnlyCollection<TodoListDto> Lists { get; init; } = [];
}

public class ColourDto
{
    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;
}
