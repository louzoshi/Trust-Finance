using Microsoft.EntityFrameworkCore;
using TrustFinance.Data;
using TrustFinance.Domain.Entities;

namespace TrustFinance.App.Services;

/// <summary>
/// Fills an empty database with a plausible year of someone's finances, so the public
/// demo opens on a working app instead of an empty state.
///
/// Every date is relative to the day it runs, so the demo never goes stale: the card
/// always has a statement that closed last month and one still open, the tax screen
/// always has months to show, and the fixed-income book always has paper that has been
/// accruing long enough for the regressive table to have stepped down.
///
/// It seeds once and only into an empty ledger. On a deployment with no persistent
/// disk that means the data comes back fresh on every restart, which is the cheapest
/// possible answer to a visitor who deletes everything.
/// </summary>
public static class DemoData
{
    public static async Task SeedAsync(TrustFinanceDbContext db, int userId, DateOnly today, ILogger logger)
    {
        if (await db.Transactions.AnyAsync(t => t.UserId == userId))
            return;

        var categories = await SeedCategoriesAsync(db, userId);
        var (checking, card, savings) = await SeedAccountsAsync(db, userId);

        await SeedLedgerAsync(db, userId, today, categories, checking, card, savings);
        await SeedRecurrencesAsync(db, userId, today, categories, checking, card);
        await SeedPortfolioAsync(db, userId, today);
        await SeedFixedIncomeAsync(db, userId, today);
        await SeedPlanningAsync(db, userId, categories);

        logger.LogInformation("Demo data seeded for user {UserId}.", userId);
    }

    private static async Task<Dictionary<string, int>> SeedCategoriesAsync(TrustFinanceDbContext db, int userId)
    {
        var names = new[]
        {
            "Salário", "Moradia", "Mercado", "Transporte", "Saúde",
            "Lazer", "Assinaturas", "Educação", "Transferências"
        };

        var categories = names.Select(n => new Category(n, userId)).ToList();
        db.Categories.AddRange(categories);
        await db.SaveChangesAsync();

        return categories.ToDictionary(c => c.Name, c => c.Id);
    }

    private static async Task<(Account Checking, Account Card, Account Savings)> SeedAccountsAsync(
        TrustFinanceDbContext db, int userId)
    {
        var checking = new Account("Conta corrente", AccountKind.Checking, userId);
        var card = new Account("Cartão Platinum", AccountKind.CreditCard, userId, closingDay: 10, dueDay: 17);
        var savings = new Account("Reserva", AccountKind.Savings, userId);

        db.Accounts.AddRange(checking, card, savings);
        await db.SaveChangesAsync();

        return (checking, card, savings);
    }

    private static async Task SeedLedgerAsync(
        TrustFinanceDbContext db, int userId, DateOnly today,
        Dictionary<string, int> categories, Account checking, Account card, Account savings)
    {
        var rows = new List<Transaction>();

        void Add(string description, decimal amount, DateOnly date, TransactionType type, string category, Account account)
            => rows.Add(new Transaction(description, amount, date, type, categories[category], account.Id, userId));

        // Eight months back, so the six-month chart is full and the tax screen has history.
        for (var back = 8; back >= 0; back--)
        {
            var month = new DateOnly(today.Year, today.Month, 1).AddMonths(-back);
            if (month > today) continue;

            DateOnly On(int day) => new(month.Year, month.Month, Math.Min(day, DateTime.DaysInMonth(month.Year, month.Month)));

            // A salary that got a raise halfway through the year.
            var salary = back >= 4 ? 11_400m : 12_650m;
            if (On(5) <= today) Add("Salário", salary, On(5), TransactionType.Income, "Salário", checking);

            if (On(8) <= today) Add("Aluguel", 2_850m, On(8), TransactionType.Expense, "Moradia", checking);
            if (On(12) <= today) Add("Condomínio", 690m, On(12), TransactionType.Expense, "Moradia", checking);
            if (On(15) <= today) Add("Energia", 180m + back * 7, On(15), TransactionType.Expense, "Moradia", checking);
            if (On(16) <= today) Add("Internet", 129.90m, On(16), TransactionType.Expense, "Moradia", checking);
            if (On(20) <= today) Add("Plano de saúde", 612m, On(20), TransactionType.Expense, "Saúde", checking);

            // Groceries across the month, some on the card and some in cash.
            if (On(3) <= today) Add("Supermercado", 480m + back * 11, On(3), TransactionType.Expense, "Mercado", card);
            if (On(14) <= today) Add("Supermercado", 395m + back * 9, On(14), TransactionType.Expense, "Mercado", card);
            if (On(24) <= today) Add("Feira", 168m, On(24), TransactionType.Expense, "Mercado", checking);

            if (On(6) <= today) Add("Combustível", 320m, On(6), TransactionType.Expense, "Transporte", card);
            if (On(19) <= today) Add("Aplicativo de transporte", 96m, On(19), TransactionType.Expense, "Transporte", card);
            if (On(11) <= today) Add("Restaurante", 210m, On(11), TransactionType.Expense, "Lazer", card);
            if (On(22) <= today) Add("Cinema", 84m, On(22), TransactionType.Expense, "Lazer", card);

            // Aporte para a reserva, uma transferência entre contas próprias.
            if (On(7) <= today)
            {
                var (outgoing, incoming) = Transaction.Transfer(
                    "Aporte na reserva", 900m, On(7), categories["Transferências"], checking.Id, savings.Id, userId);
                rows.Add(outgoing);
                rows.Add(incoming);
            }

            // Pagamento da fatura do mês anterior, também uma transferência.
            if (On(17) <= today && back < 8)
            {
                var (outgoing, incoming) = Transaction.Transfer(
                    "Pagamento da fatura", 1_480m, On(17), categories["Transferências"], checking.Id, card.Id, userId);
                rows.Add(outgoing);
                rows.Add(incoming);
            }
        }

        // A purchase split over five months, the kind a card exists for.
        var notebookStart = new DateOnly(today.Year, today.Month, 1).AddMonths(-3);
        rows.AddRange(Transaction.Installments(
            "Notebook", 7_490m, notebookStart, TransactionType.Expense,
            categories["Educação"], card.Id, userId, count: 5));

        // A few one-off lines so the recent list is not all repetition.
        Add("Dentista", 480m, today.AddDays(-9), TransactionType.Expense, "Saúde", checking);
        Add("Curso de inglês", 389m, today.AddDays(-21), TransactionType.Expense, "Educação", checking);
        Add("Freelance", 3_200m, today.AddDays(-14), TransactionType.Income, "Salário", checking);
        Add("Presente de aniversário", 260m, today.AddDays(-4), TransactionType.Expense, "Lazer", card);

        db.Transactions.AddRange(rows.Where(r => r.IsValid && r.Date <= today));
        await db.SaveChangesAsync();
    }

    private static async Task SeedRecurrencesAsync(
        TrustFinanceDbContext db, int userId, DateOnly today,
        Dictionary<string, int> categories, Account checking, Account card)
    {
        var start = new DateOnly(today.Year, today.Month, 1).AddMonths(-8);

        // Already posted above, so these carry a next date in the future: they show the
        // feature without doubling the ledger.
        var next = new DateOnly(today.Year, today.Month, 1).AddMonths(1);

        var recurrences = new[]
        {
            new RecurringTransaction("Assinatura de streaming", 55.90m, TransactionType.Expense,
                categories["Assinaturas"], card.Id, Frequency.Monthly, new DateOnly(next.Year, next.Month, 4), null, userId),
            new RecurringTransaction("Academia", 149m, TransactionType.Expense,
                categories["Saúde"], card.Id, Frequency.Monthly, new DateOnly(next.Year, next.Month, 6), null, userId),
            new RecurringTransaction("Seguro do carro", 2_340m, TransactionType.Expense,
                categories["Transporte"], checking.Id, Frequency.Yearly, new DateOnly(next.Year, next.Month, 20), null, userId),
            new RecurringTransaction("Faxina", 180m, TransactionType.Expense,
                categories["Moradia"], checking.Id, Frequency.Weekly,
                start.AddDays(2), start.AddMonths(3), userId)
        };

        db.RecurringTransactions.AddRange(recurrences.Where(r => r.IsValid));
        await db.SaveChangesAsync();
    }

    private static async Task SeedPortfolioAsync(TrustFinanceDbContext db, int userId, DateOnly today)
    {
        var start = today.AddMonths(-14);

        Trade Buy(string ticker, AssetClass c, decimal qty, decimal price, decimal fees, int monthsAgo)
            => new(ticker, c, TradeSide.Buy, qty, price, fees, today.AddMonths(-monthsAgo), userId);

        Trade Sell(string ticker, AssetClass c, decimal qty, decimal price, decimal fees, int monthsAgo)
            => new(ticker, c, TradeSide.Sell, qty, price, fees, today.AddMonths(-monthsAgo), userId);

        var trades = new List<Trade>
        {
            Buy("PETR4", AssetClass.Stock, 800, 31.40m, 4.90m, 14),
            Buy("VALE3", AssetClass.Stock, 300, 58.20m, 4.90m, 13),
            Buy("ITSA4", AssetClass.Stock, 1500, 9.85m, 4.90m, 12),
            Buy("HGLG11", AssetClass.Fii, 90, 158.00m, 2.50m, 11),
            Buy("BOVA11", AssetClass.Etf, 60, 118.40m, 2.00m, 10),
            Buy("KNRI11", AssetClass.Fii, 70, 142.30m, 2.50m, 8),
            Buy("MGLU3", AssetClass.Stock, 400, 19.60m, 2.00m, 7),

            // Uma venda acima do teto de isenção, recente o bastante para o DARF ainda
            // estar em aberto: R$ 23.880 passa dos R$ 20.000 e o mês vira tributado.
            Sell("PETR4", AssetClass.Stock, 600, 39.80m, 4.90m, 1),

            // E uma venda pequena, que fica isenta por ficar abaixo do teto no seu mês.
            Sell("ITSA4", AssetClass.Stock, 400, 11.20m, 2.00m, 4),
            Buy("VALE3", AssetClass.Stock, 100, 61.00m, 2.00m, 3),
            Sell("VALE3", AssetClass.Stock, 150, 63.80m, 2.00m, 3),

            Buy("BBAS3", AssetClass.Stock, 300, 27.10m, 2.00m, 2)
        };

        db.Trades.AddRange(trades.Where(t => t.IsValid && t.Date >= start));

        // Um desdobramento, para o preço médio mostrar que sabe lidar com ele.
        db.CorporateActions.Add(new CorporateAction(
            "MGLU3", CorporateActionKind.Split, today.AddMonths(-5), 2m, null, userId));

        // E uma bonificação ao custo declarado pela empresa.
        db.CorporateActions.Add(new CorporateAction(
            "ITSA4", CorporateActionKind.Bonus, today.AddMonths(-9), 0.05m, 7.20m, userId));

        // Proventos: FII todo mês, dividendo e JCP de vez em quando.
        var payouts = new List<Payout>();
        for (var back = 11; back >= 0; back--)
        {
            var date = today.AddMonths(-back);
            payouts.Add(new Payout("HGLG11", PayoutKind.FundIncome, date, 90, 99.00m, 0m, userId));
            if (back <= 8)
                payouts.Add(new Payout("KNRI11", PayoutKind.FundIncome, date, 70, 61.60m, 0m, userId));
        }

        payouts.Add(new Payout("PETR4", PayoutKind.Dividend, today.AddMonths(-10), 800, 1_120.00m, 0m, userId));
        payouts.Add(new Payout("PETR4", PayoutKind.Dividend, today.AddMonths(-3), 300, 486.00m, 0m, userId));
        payouts.Add(new Payout("ITSA4", PayoutKind.InterestOnEquity, today.AddMonths(-7), 1575, 378.00m, 56.70m, userId));
        payouts.Add(new Payout("ITSA4", PayoutKind.InterestOnEquity, today.AddMonths(-1), 1175, 293.75m, 44.06m, userId));
        payouts.Add(new Payout("VALE3", PayoutKind.Dividend, today.AddMonths(-5), 300, 840.00m, 0m, userId));

        db.Payouts.AddRange(payouts.Where(p => p.IsValid));
        await db.SaveChangesAsync();
    }

    private static async Task SeedFixedIncomeAsync(TrustFinanceDbContext db, int userId, DateOnly today)
    {
        var papers = new[]
        {
            // Dois papéis no mesmo emissor, de propósito: juntos passam do teto do FGC.
            new FixedIncomeInvestment("Banco Inter", FixedIncomeKind.Cdb, IndexKind.PercentOfCdi,
                110m, 90_000m, today.AddMonths(-16), today.AddMonths(8), userId, hasDailyLiquidity: true),
            new FixedIncomeInvestment("Banco Inter", FixedIncomeKind.Cdb, IndexKind.PercentOfCdi,
                104m, 180_000m, today.AddMonths(-11), today.AddMonths(21), userId),

            // Um prefixado, que é o único que dá para marcar a mercado.
            new FixedIncomeInvestment("Banco Master", FixedIncomeKind.Cdb, IndexKind.Fixed,
                13.4m, 60_000m, today.AddMonths(-13), today.AddMonths(23), userId),

            // Isento, para a tela mostrar por que 92% do CDI pode ganhar de 110%.
            new FixedIncomeInvestment("Banco BTG", FixedIncomeKind.Lci, IndexKind.PercentOfCdi,
                92m, 45_000m, today.AddMonths(-7), today.AddMonths(11), userId),

            new FixedIncomeInvestment("Tesouro Nacional", FixedIncomeKind.TesouroSelic, IndexKind.SelicPlus,
                0m, 38_000m, today.AddMonths(-19), today.AddYears(3), userId, hasDailyLiquidity: true),
            new FixedIncomeInvestment("Tesouro Nacional", FixedIncomeKind.TesouroPrefixado, IndexKind.Fixed,
                11.9m, 25_000m, today.AddMonths(-16), today.AddYears(4), userId),
            new FixedIncomeInvestment("Tesouro Nacional", FixedIncomeKind.TesouroIpca, IndexKind.IpcaPlus,
                6.05m, 30_000m, today.AddMonths(-25), today.AddYears(9), userId)
        };

        db.FixedIncomeInvestments.AddRange(papers.Where(p => p.IsValid));
        await db.SaveChangesAsync();
    }

    private static async Task SeedPlanningAsync(TrustFinanceDbContext db, int userId, Dictionary<string, int> categories)
    {
        var budgets = new[]
        {
            new Budget(categories["Mercado"], 1_200m, userId),
            new Budget(categories["Lazer"], 500m, userId),
            new Budget(categories["Transporte"], 600m, userId),
            new Budget(categories["Moradia"], 4_000m, userId)
        };

        db.Budgets.AddRange(budgets.Where(b => b.IsValid));

        var settings = await db.UserSettings.FirstOrDefaultAsync(s => s.UserId == userId);
        if (settings is null)
        {
            settings = new UserSettings { UserId = userId };
            db.UserSettings.Add(settings);
        }

        settings.MonthlyContributionGoal = 2_500m;
        settings.EmergencyFundGoal = 60_000m;

        await db.SaveChangesAsync();
    }
}
