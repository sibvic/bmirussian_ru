using Sibvic.AuthLib;
using Sibvic.AuthLib.Logic;
using Sibvic.UserWithBalanceLib;
using Sibvic.UserWithBalanceLib.Data;

namespace BMIRussian_ru.Logic
{
    public class WelcomeBalanceCallback(BalanceManager balanceManager, UserWithBalanceContext context) : IAuthLogicCallback
    {
        public void AfterUserAdded(User user)
        {
            balanceManager.Topup(user.Id, 100m, "Приветственный бонус");
            context.SaveChanges();
        }

        public void BeforeUserAdded(User user)
        {
            //do nothing
        }
    }
}
