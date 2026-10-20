namespace Njord.OpenMeteo;

public interface IBudgetProvider
{
    BudgetRate GetCurrentRate();
}
