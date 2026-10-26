namespace Njord.OpenMeteo.Budget;

public interface IBudgetProvider
{
    BudgetRate GetCurrentRate();
}
