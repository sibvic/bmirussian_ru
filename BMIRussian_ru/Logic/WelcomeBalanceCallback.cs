using Sibvic.AuthLib;
using Sibvic.AuthLib.Logic;
using Sibvic.UserWithBalanceLib;

namespace BMIRussian_ru.Logic
{
    public class WelcomeBalanceCallback(BalanceManager balanceManager) : IAuthLogicCallback
    {
        public void BeforeUserAdded(User user)
        {
            balanceManager.Topup(user.Id, 100m, "Приветственный бонус");
        }
    }
}
