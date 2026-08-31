using System.Collections;
using System.Globalization;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ScheduleBot.Models;

namespace ScheduleBot.Services;

public class TransactionService(AppDbContext dbContext, MainService service) : DatabaseService(dbContext, service)
{
    private readonly AppDbContext _dbContext = dbContext;
    
    public async Task<Guid> CreateNewWallet(long chatId, string walletName)
    {
        var user = await GetUserByTelId(chatId);
        
        var wallet = new Wallet
        {
            Id = Guid.NewGuid(),
            Name = walletName,
            CreatorId = user!.Id
        };
        
        var walletAccess = new WalletAccess
        {
            Id = Guid.NewGuid(),
            WalletId = wallet.Id,
            UserId = user.Id
        };
        
        var category = new Category
        {
            Id = Guid.NewGuid(),
            WalletId = wallet.Id,
            CreateTime = GetIranDateTime(),
            Name = "Not specified",
            TempAdded = false,
            TempDeleted = false
        };
        
        _dbContext.Wallet.Add(wallet);
        _dbContext.WalletAccess.Add(walletAccess);
        _dbContext.WalletCategory.Add(category);
        await _dbContext.SaveChangesAsync();
        return wallet.Id;
    }
    
    public async Task<Wallet?> GetWalletByWalletId(Guid walletId)
    {
        return await _dbContext.Wallet.FirstOrDefaultAsync(w => w.Id == walletId);
    }
    
    public async Task<List<Wallet>> GetWalletsByTelId(long chatId = 0)
    {
        var user = await GetUserByTelId(chatId);
        var walletAccesses = await _dbContext.WalletAccess.Where(w => w.UserId == user!.Id).Select(w => w.WalletId).ToListAsync();
        return await _dbContext.Wallet.Where(w => walletAccesses.Contains(w.Id)).ToListAsync();
    }

    public async Task<Wallet?> GetWalletForUser(Guid walletId, long chatId)
    {
        var user = await GetUserByTelId(chatId);
        return user == null ? null : await _dbContext.WalletAccess
            .Where(a => a.WalletId == walletId && a.UserId == user.Id)
            .Join(_dbContext.Wallet, a => a.WalletId, w => w.Id, (_, w) => w)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> InviteAccept(long chatId, Guid walletId)
    {
        var wallet = await _dbContext.Wallet.FindAsync(walletId);
        var user = await GetUserByTelId(chatId);
        if (wallet == null || user == null) return false;
        if (await _dbContext.WalletAccess.AnyAsync(a => a.WalletId == walletId && a.UserId == user.Id)) return false;
        _dbContext.WalletAccess.Add(new WalletAccess { Id = Guid.NewGuid(), WalletId = walletId, UserId = user.Id });
        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<List<Category>> GetCategories(Guid walletId)
    {
        return await _dbContext.WalletCategory.Where(r => r.WalletId == walletId).ToListAsync();
    }

    public async Task<Category?> GetDefaultCategory(Guid walletId)
    {
        return await _dbContext.WalletCategory.FirstOrDefaultAsync(r => r.WalletId == walletId);
    }

    public async Task<List<Category>> GetCategoriesByWalletIdAll(Guid walletId)
    {
        return await _dbContext.WalletCategory.Where(c => c.WalletId == walletId).OrderBy(c => c.CreateTime).ToListAsync();
    }

    public async Task<List<Category>> GetCategoriesByWalletId(Guid walletId)
    {
        return (await GetCategoriesByWalletIdAll(walletId)).Where(c => c is { TempAdded: false, TempDeleted: false }).ToList();
    }

    public async Task<Guid> AddCategoryToWallet(Guid walletId, string name)
    {
        var category = new Category { Id = Guid.NewGuid(), Name = name.Trim() , WalletId = walletId, CreateTime = GetIranDateTime(), TempAdded = true};
        _dbContext.WalletCategory.Add(category);
        await _dbContext.SaveChangesAsync();
        return category.Id;
    }
    
    public async Task<Tuple<List<string>, List<string>, List<string>>> LoadCategoryServiceChanges(Guid walletId)
    {
        var categories = await GetCategoriesByWalletIdAll(walletId);
        var added = categories.Where(p => p is { TempAdded: true, TempDeleted: false }).ToList();
        var deleted = categories.Where(p => p is { TempAdded: false, TempDeleted: true }).ToList();
        var both = categories.Where(p => p is { TempAdded: true, TempDeleted: true }).ToList();
        return new Tuple<List<string>, List<string>, List<string>>(
            added.Select(x => x.Name!).ToList(),
            deleted.Select(x => x.Name!).ToList(),
            both.Select(x => x.Name!).ToList());
    }
    
    public async Task<bool> SubmitCategoryServiceChanges(Guid walletId)
    {
        var categories = await GetCategoriesByWalletIdAll(walletId);
        var added = categories.Where(p => p is { TempAdded: true, TempDeleted: false });
        foreach (var category in added) category.TempAdded = false;
        return await _dbContext.WalletCategory.Where(p => p.TempDeleted).ExecuteDeleteAsync() + await _dbContext.SaveChangesAsync() > 0;
    }

    public async Task<bool> CancelCategoryServiceChanges(Guid walletId)
    {
        var categories = await GetCategoriesByWalletId(walletId);
        var deleted = categories.Where(p => p.TempDeleted);
        foreach (var category in deleted) category.TempDeleted = false;
        return  await _dbContext.WalletCategory.Where(p => p.TempAdded).ExecuteDeleteAsync() + await _dbContext.SaveChangesAsync() > 0;
    }

    public async Task<IEnumerable<User>> GetUsersWithAccessByWalletId(Guid walletId)
    {
        var walletAccess = await GetCartAccessByWalletId(walletId);
        return await GetUsersByIds(walletAccess.Select(x => x.UserId).ToList());
    }
    
    public async Task<List<WalletAccess>> GetCartAccessByWalletId(Guid walletId)
    {
        return await _dbContext.WalletAccess.Where(c => c.WalletId == walletId).ToListAsync();
    }

    public async Task<bool> DeleteCategoryFromWallet(Guid categoryId)
    {
        var category = await GetCategoryByCategoryId(categoryId);
        category!.TempDeleted = !category.TempDeleted;
        return await _dbContext.SaveChangesAsync() > 0;
    }

    public async Task<Category?> GetCategoryByCategoryId(Guid categoryId)
    {
        return await _dbContext.WalletCategory.FirstOrDefaultAsync(c => c.Id == categoryId);
    }

    public async Task<bool> AddTransaction(long chatId, Guid walletId, TransactionProcess transactionProcess)
    {
        var user = await GetUserByTelId(chatId);
        if (user == null || await GetWalletForUser(walletId, chatId) == null ||
            !await _dbContext.WalletCategory.AnyAsync(r => r.WalletId == walletId && r.Id == transactionProcess.CategoryId)) return false;
        _dbContext.WalletTransactions.Add(new TransactionRecord
        {
            Id = Guid.NewGuid(),
            WalletId = walletId,
            ConsumerId = user.Id,
            CategoryId = transactionProcess.CategoryId,
            Date = transactionProcess.Date,
            Deposit = transactionProcess.Deposit,
            Withdraw = transactionProcess.Withdraw,
            BalanceAfter = transactionProcess.BalanceAfter,
            DocumentNo = transactionProcess.DocumentNo,
            Title = transactionProcess.Title,
        });
        return await _dbContext.SaveChangesAsync() > 0;
    }

    public async Task<bool> GetTransactionByDocumentNo(long chatId, long documentNo, Guid walletId)
    {
        var user =  await GetUserByTelId(chatId);
        return await _dbContext.WalletTransactions.FirstOrDefaultAsync(c => c.ConsumerId == user!.Id && c.DocumentNo == documentNo && c.WalletId == walletId) != null;
    }

    public async Task<List<TransactionRecord>> GetTransactionByWalletAndUser(long chatId, Guid walletId)
    {
        var user =  await GetUserByTelId(chatId);
        return await _dbContext.WalletTransactions.Where(c => c.ConsumerId == user!.Id && c.WalletId == walletId).ToListAsync();
    }
    
    public async Task<WalletReport> GetReportData(Guid walletId, List<Guid> categoryIds, DateTime? startDate = null, DateTime? endDate = null)
    {
        #region LoadDatas

        var wallet = await GetWalletByWalletId(walletId);
        if (wallet == null) throw new Exception("Wallet not found");
    
        var allCategories = await GetCategoriesByWalletId(walletId);
        var categories = allCategories.Where(c => !c.TempDeleted).ToList();
    
        var usersWithAccess = await GetUsersWithAccessByWalletId(walletId);
        var userList = usersWithAccess.ToList();

        var query = _dbContext.WalletTransactions
            .AsNoTracking()
            .Where(t => t.WalletId == walletId)
            .Where(t => t.Date >= (startDate ?? DateTime.MinValue))
            .Where(t => t.Date <= (endDate ?? DateTime.MaxValue));
    
        if (categoryIds.Count != 0) query = query.Where(t => categoryIds.Contains(t.CategoryId));
    
        var transactions = await query
            .OrderBy(t => t.Date)
            .ToListAsync();
        
        if (transactions.Count == 0) throw new Exception("No Transaction Available for this period");

        var transactionData = await query
            .GroupBy(t => t.ConsumerId)
            .Select(g => new
            {
                ConsumerId = g.Key,
                FirstTransaction = g.OrderBy(t => t.Date).FirstOrDefault(),
                LastTransaction = g.OrderByDescending(t => t.Date).FirstOrDefault(),
                TransactionCount = g.Count(),
            })
            .ToListAsync();
        var userIds = transactionData.Select(x => x.ConsumerId).ToList();
        var users = await _dbContext.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Name);
        var userTransactionRange = transactionData
            .Select(t => new
            {
                t.FirstTransaction,
                t.LastTransaction,
                t.TransactionCount,
                UserName = users.TryGetValue(t.ConsumerId, out var name) ? name : null
            })
            .ToList();
    
        var userMap = userList.ToDictionary(u => u.Id, u => u);
    
        var transactionsByCategory = transactions
            .GroupBy(t => t.CategoryId)
            .ToDictionary(g => g.Key, g => g.ToList());
        
        #endregion

        #region BuildSummary
        
        var deposits = transactions.Where(t => t.Deposit > 0).ToList();
        var withdrawals = transactions.Where(t => t.Withdraw > 0).ToList();
        
        var summary = new ReportSummary
        {
            TotalDeposits = deposits.Sum(t => t.Deposit),
            TotalWithdrawals = withdrawals.Sum(t => t.Withdraw),
            DepositCount = deposits.Count,
            WithdrawCount = withdrawals.Count,
            NetCashFlow = deposits.Sum(t => t.Deposit) - withdrawals.Sum(t => t.Withdraw),
            AverageDeposit = deposits.Any() ? deposits.Average(t => t.Deposit) : 0,
            AverageWithdrawal = withdrawals.Any() ? withdrawals.Average(t => t.Withdraw) : 0
        };
        
        #endregion

        #region balanceReport
        
        var balanceReport = (from user in userTransactionRange
            let first = user.FirstTransaction
            let last = user.LastTransaction
            select new BalanceReport
            {
                ConsumerName = user.UserName!,
                TransactionCount = user.TransactionCount,
                FirstBalance = first!.BalanceAfter + first.Withdraw - first.Deposit,
                LastBalance = last!.BalanceAfter,
                Overall = last!.BalanceAfter - (first!.BalanceAfter + first.Withdraw - first.Deposit)
            }).ToList();

        var br = balanceReport.ToList();
        balanceReport.Add(new BalanceReport
        {
            ConsumerName = "All",
            TransactionCount = br.Sum(x => x.TransactionCount),
            FirstBalance = br.Sum(x => x.FirstBalance),
            LastBalance = br.Sum(x => x.LastBalance),
            Overall = br.Sum(x => x.Overall),
        });
        
        #endregion

        #region BuildCategoryReports
        
        var categoryReports = new List<CategoryReport>();
        var totalWithdrawals = transactions.Where(t => t.Withdraw > 0).Sum(t => t.Withdraw);
        
        foreach (var category in categories)
        {
            if (!transactionsByCategory.TryGetValue(category.Id, out var value) || value.Count == 0) continue;
            var categoryTransactions = transactionsByCategory[category.Id];
            var categoryDeposits = categoryTransactions.Where(t => t.Deposit > 0).ToList();
            var categoryWithdrawals = categoryTransactions.Where(t => t.Withdraw > 0).ToList();
            
            var categoryReport = new CategoryReport
            {
                CategoryId = category.Id,
                CategoryName = category.Name ?? "Unnamed Category",
                TransactionCount = categoryTransactions.Count,
                TotalDeposits = categoryDeposits.Sum(t => t.Deposit),
                TotalWithdrawals = categoryWithdrawals.Sum(t => t.Withdraw),
                NetCashFlow = categoryDeposits.Sum(t => t.Deposit) - categoryWithdrawals.Sum(t => t.Withdraw),
                PercentageOfTotalWithdrawals = totalWithdrawals > 0 ? (categoryWithdrawals.Sum(t => t.Withdraw) / totalWithdrawals) * 100 : 0,
            };
            categoryReports.Add(categoryReport);
        }
        
        categoryReports =  categoryReports.OrderByDescending(r => r.TotalWithdrawals).ToList();
        
        #endregion

        #region BuildMonthlyReports (Persian Months - Alternative)

        var monthlyReports = new List<MonthlyReport>();
        var persianCalendar = new PersianCalendar();
        var persianCulture = new CultureInfo("fa-IR");
        var monthlyGroups = transactions
            .GroupBy(t => new 
            { 
                Year = persianCalendar.GetYear(t.Date), 
                Month = persianCalendar.GetMonth(t.Date) 
            })
            .OrderBy(g => g.Key.Year)
            .ThenBy(g => g.Key.Month);

        foreach (var group in monthlyGroups)
        {
            var monthlyTransactions = group.ToList();
            var monthDeposits = monthlyTransactions.Where(t => t.Deposit > 0).Sum(t => t.Deposit);
            var monthWithdrawals = monthlyTransactions.Where(t => t.Withdraw > 0).Sum(t => t.Withdraw);
            var netCashFlow = monthDeposits - monthWithdrawals;
            var persianDate = new DateTime(group.Key.Year, group.Key.Month, 1, persianCalendar);
    
            var report = new MonthlyReport
            {
                Year = group.Key.Year,
                Month = group.Key.Month,
                MonthName = persianDate.ToString("MMMM", persianCulture), // Gets Persian month name
                TransactionCount = monthlyTransactions.Count,
                Deposits = monthDeposits,
                Withdrawals = monthWithdrawals,
                NetCashFlow = netCashFlow,
                ClosingBalance = monthlyTransactions.Last().BalanceAfter
            };
            monthlyReports.Add(report);
        }

        #endregion

        #region BuildUserReports
        
        var userReports = new List<UserReport>();
        var userGroups = transactions
            .GroupBy(t => t.ConsumerId)
            .Select(g => new
            {
                UserId = g.Key,
                Transactions = g.ToList()
            });
        
        foreach (var group in userGroups)
        {
            var user = userMap.TryGetValue(group.UserId, out var value) ? value : null;
            var userName = user?.Name ?? "Unknown User";
            var userDeposits = group.Transactions.Where(t => t.Deposit > 0).Sum(t => t.Deposit);
            var userWithdrawals = group.Transactions.Where(t => t.Withdraw > 0).Sum(t => t.Withdraw);
            
            var report = new UserReport
            {
                UserId = group.UserId,
                UserName = userName,
                TransactionCount = group.Transactions.Count,
                Deposits = userDeposits,
                Withdrawals = userWithdrawals,
                NetCashFlow = userDeposits - userWithdrawals
            };
            userReports.Add(report);
        }
        userReports = userReports.OrderByDescending(r => r.NetCashFlow).ToList();
        
        #endregion
        
        return new WalletReport
        {
            WalletName = wallet.Name ?? "Unnamed Wallet",
            GeneratedAt = GetIranDateTime(),
            Transactions = transactions,
            TotalTransactions = transactions.Count,
            FromDate = startDate ?? (transactions.Count != 0 ? transactions.Min(t => t.Date) : null),
            ToDate = endDate ?? (transactions.Count != 0 ? transactions.Max(t => t.Date) : null),
            Summary = summary,
            BalanceReports = balanceReport,
            CategoryReports = categoryReports,
            MonthlyReports = monthlyReports,
            UserReports = userReports
        };
    }
    
    
    public byte[] GeneratePdf(WalletReport report)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.CheckIfAllTextGlyphsAreAvailable = false;
        var fontPath = Path.Combine(AppContext.BaseDirectory, "Fonts", "Vazirmatn-Regular.ttf");
        var boldFontPath = Path.Combine(AppContext.BaseDirectory, "Fonts", "Vazirmatn-Bold.ttf");
        FontManager.RegisterFontWithCustomName("Vazirmatn", File.OpenRead(fontPath));
        FontManager.RegisterFontWithCustomName("Vazirmatn", File.OpenRead(boldFontPath));
        
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(40);
                page.Size(PageSizes.A4);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontFamily("Vazirmatn", "Noto Emoji").FontSize(9));
                page.DefaultTextStyle(x => x.FontSize(9));
                page.Header().Element(ComposeHeader);
                page.Content().Element(ComposeContent);
                page.Footer().Element(ComposeFooter);
                return;

                void ComposeHeader(IContainer innerContainer)
                {
                    innerContainer.Column(col =>
                    {
                        col.Spacing(5);
                        col.Item().Text("📊 WALLET FINANCIAL REPORT")
                            .FontSize(20)
                            .Bold()
                            .FontColor(Colors.Blue.Darken3);
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(infoCol =>
                            {
                                infoCol.Item().Text($"Wallet: {report.WalletName}").FontSize(12);
                                infoCol.Item().Text($"Generated: {report.GeneratedAt:yyyy/MM/dd HH:mm:ss}").FontSize(10).FontColor(Colors.Grey.Medium);
                                if (report.FromDate.HasValue && report.ToDate.HasValue)
                                {
                                    infoCol.Item().Text($"Period: {report.FromDate:yyyy/MM/dd} - {report.ToDate:yyyy/MM/dd}").FontSize(10);
                                }
                                infoCol.Item().Text($"Total Transactions: {report.TotalTransactions}").FontSize(11).Bold();
                            });
                            
                            row.ConstantItem(150).Border(1).BorderColor(Colors.Grey.Lighten1).Padding(5).Column(summaryBox =>
                            {
                                summaryBox.Item().Text("Quick Stats").FontSize(10).Bold().AlignCenter();
                                summaryBox.Item().Text($"Deposits: {report.Summary.TotalDeposits:N0}").FontSize(9);
                                summaryBox.Item().Text($"Withdrawals: {report.Summary.TotalWithdrawals:N0}").FontSize(9);
                                summaryBox.Item().Text($"Net: {report.Summary.NetCashFlow:N0}").FontSize(9)
                                    .FontColor(report.Summary.NetCashFlow >= 0 ? Colors.Green.Darken2 : Colors.Red.Darken2);
                            });
                        });
                    });
                }
                
                void ComposeContent(IContainer innerContainer)
                {
                    innerContainer.Column(col =>
                    {
                        col.Spacing(15);
                        col.Item().Element(ComposeFinancialSummary);
                        if (report.BalanceReports.Any()) col.Item().Element(ComposeBalanceReport);
                        if (report.CategoryReports.Any()) col.Item().Element(ComposeCategoryAnalysis);
                        if (report.MonthlyReports.Any()) col.Item().Element(ComposeMonthlySummary);
                        if (report.UserReports.Any()) col.Item().Element(ComposeUserActivity);
                        if (report.Transactions.Any()) col.Item().Element(ComposeTransactionDetails);
                    });
                }
                
                void ComposeFinancialSummary(IContainer innerContainer)
                {
                    innerContainer.Border(1).BorderColor(Colors.Grey.Lighten2).Background(Colors.Grey.Lighten4).Padding(10).Column(col =>
                    {
                        col.Spacing(8);
                        col.Item().Text("📈 FINANCIAL SUMMARY").FontSize(14).Bold().FontColor(Colors.Blue.Darken2);
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                            });
                            
                            table.Header(header =>
                            {
                                header.Cell().Background(Colors.Blue.Lighten4).Padding(5).Text("Metric").Bold().FontSize(10);
                                header.Cell().Background(Colors.Blue.Lighten4).Padding(5).Text("Amount").Bold().AlignRight().FontSize(10);
                                header.Cell().Background(Colors.Blue.Lighten4).Padding(5).Text("Count").Bold().AlignRight().FontSize(10);
                                header.Cell().Background(Colors.Blue.Lighten4).Padding(5).Text("Average").Bold().AlignRight().FontSize(10);
                            });

                            AddRow("Deposits", report.Summary.TotalDeposits, report.Summary.DepositCount, report.Summary.AverageDeposit);
                            AddRow("Withdrawals", report.Summary.TotalWithdrawals, report.Summary.WithdrawCount, report.Summary.AverageWithdrawal);
                            
                            table.Cell().Padding(3).Text("Net Cash Flow").Bold();
                            table.Cell().Padding(3).Text($"{report.Summary.NetCashFlow:N0}").AlignRight().FontColor(report.Summary.NetCashFlow >= 0 ? Colors.Green.Darken2 : Colors.Red.Darken2).Bold();
                            table.Cell().Padding(3).Text("").AlignRight();
                            table.Cell().Padding(3).Text("").AlignRight();
                            return;
                            
                            void AddRow(string metric, decimal amount, int count, decimal avg)
                            {
                                table.Cell().Padding(3).Text(metric);
                                table.Cell().Padding(3).Text($"{amount:N0}").AlignRight();
                                table.Cell().Padding(3).Text($"{count}").AlignRight();
                                table.Cell().Padding(3).Text($"{avg:N0}").AlignRight();
                            }
                        });
                    });
                }
                
                void ComposeBalanceReport(IContainer innerContainer)
                {
                    innerContainer.Border(1).BorderColor(Colors.Grey.Lighten2).Background(Colors.Grey.Lighten4).Padding(10).Column(col =>
                    {
                        col.Spacing(8);
                        col.Item().Text("👥 BALANCE REPORT").FontSize(14).Bold().FontColor(Colors.Blue.Darken2);
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                            });
                            
                            table.Header(header =>
                            {
                                header.Cell().Background(Colors.Blue.Lighten4).Padding(5).Text("Consumer").Bold().FontSize(10);
                                header.Cell().Background(Colors.Blue.Lighten4).Padding(5).Text("Transactions").Bold().AlignRight().FontSize(10);
                                header.Cell().Background(Colors.Blue.Lighten4).Padding(5).Text("First Balance").Bold().AlignRight().FontSize(10);
                                header.Cell().Background(Colors.Blue.Lighten4).Padding(5).Text("Last Balance").Bold().AlignRight().FontSize(10);
                                header.Cell().Background(Colors.Blue.Lighten4).Padding(5).Text("Overall").Bold().AlignRight().FontSize(10);
                            });

                            foreach (var item in report.BalanceReports.Where(x => x.ConsumerName != "All"))
                            {
                                AddRow(item.ConsumerName, item.TransactionCount, item.FirstBalance, item.LastBalance, item.Overall);
                            }

                            var allItem = report.BalanceReports.FirstOrDefault(x => x.ConsumerName == "All");
                            if (allItem != null)
                            {
                                table.Cell().Padding(3).Text("All").Bold();
                                table.Cell().Padding(3).Text($"{allItem.TransactionCount}").AlignRight().Bold();
                                table.Cell().Padding(3).Text($"{allItem.FirstBalance:N0}").AlignRight().Bold();
                                table.Cell().Padding(3).Text($"{allItem.LastBalance:N0}").AlignRight().Bold();
                                table.Cell().Padding(3).Text($"{allItem.Overall:N0}").AlignRight()
                                    .FontColor(allItem.Overall >= 0 ? Colors.Green.Darken2 : Colors.Red.Darken2).Bold();
                            }

                            void AddRow(string name, int count, decimal firstBal, decimal lastBal, decimal overall)
                            {
                                table.Cell().Padding(3).Text(name);
                                table.Cell().Padding(3).Text($"{count}").AlignRight();
                                table.Cell().Padding(3).Text($"{firstBal:N0}").AlignRight();
                                table.Cell().Padding(3).Text($"{lastBal:N0}").AlignRight();
                                table.Cell().Padding(3).Text($"{overall:N0}").AlignRight()
                                    .FontColor(overall >= 0 ? Colors.Green.Darken2 : Colors.Red.Darken2);
                            }
                        });
                    });
                }
                
                void ComposeCategoryAnalysis(IContainer innerContainer)
                {
                    innerContainer.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(col =>
                    {
                        col.Spacing(8);
                        col.Item().Text("📊 CATEGORY ANALYSIS").FontSize(14).Bold().FontColor(Colors.Blue.Darken2);
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                            });
                            
                            table.Header(header =>
                            {
                                header.Cell().Background(Colors.Blue.Lighten4).Padding(5).Text("Category").Bold().FontSize(9);
                                header.Cell().Background(Colors.Blue.Lighten4).Padding(5).Text("Count").Bold().AlignRight().FontSize(9);
                                header.Cell().Background(Colors.Blue.Lighten4).Padding(5).Text("Deposits").Bold().AlignRight().FontSize(9);
                                header.Cell().Background(Colors.Blue.Lighten4).Padding(5).Text("Withdrawals").Bold().AlignRight().FontSize(9);
                                header.Cell().Background(Colors.Blue.Lighten4).Padding(5).Text("Net").Bold().AlignRight().FontSize(9);
                                header.Cell().Background(Colors.Blue.Lighten4).Padding(5).Text("% of Total").Bold().AlignRight().FontSize(9);
                            });
                            
                            var rowIndex = 0;
                            foreach (var categoryReport in report.CategoryReports)
                            {
                                var backgroundColor = rowIndex++ % 2 == 0 ? Colors.Grey.Lighten4 : Colors.White;
                                table.Cell().Padding(3).Background(backgroundColor).Text(categoryReport.CategoryName);
                                table.Cell().Padding(3).Background(backgroundColor).Text($"{categoryReport.TransactionCount}").AlignRight();
                                table.Cell().Padding(3).Background(backgroundColor).Text($"{categoryReport.TotalDeposits:N0}").AlignRight();
                                table.Cell().Padding(3).Background(backgroundColor).Text($"{categoryReport.TotalWithdrawals:N0}").AlignRight();
                                table.Cell().Padding(3).Background(backgroundColor).Text($"{categoryReport.NetCashFlow:N0}").AlignRight().FontColor(categoryReport.NetCashFlow >= 0 ? Colors.Green.Darken2 : Colors.Red.Darken2);
                                table.Cell().Padding(3).Background(backgroundColor).Text($"{categoryReport.PercentageOfTotalWithdrawals:F1}%").AlignRight();
                            }
                            
                            table.Cell().Padding(3).Text("TOTAL").Bold();
                            table.Cell().Padding(3).Text($"{report.CategoryReports.Sum(c => c.TransactionCount)}").AlignRight().Bold();
                            table.Cell().Padding(3).Text($"{report.CategoryReports.Sum(c => c.TotalDeposits):N0}").AlignRight().Bold();
                            table.Cell().Padding(3).Text($"{report.CategoryReports.Sum(c => c.TotalWithdrawals):N0}").AlignRight().Bold();
                            table.Cell().Padding(3).Text($"{report.CategoryReports.Sum(c => c.NetCashFlow):N0}").AlignRight().Bold();
                            table.Cell().Padding(3).Text("100%").AlignRight().Bold();
                        });
                    });
                }
                
                void ComposeMonthlySummary(IContainer innerContainer)
                {
                    innerContainer.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(col =>
                    {
                        col.Spacing(8);
                        col.Item().Text("📅 MONTHLY SUMMARY").FontSize(14).Bold().FontColor(Colors.Blue.Darken2);
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                            });
                            
                            table.Header(header =>
                            {
                                header.Cell().Background(Colors.Blue.Lighten4).Padding(5).Text("Month").Bold().FontSize(9);
                                header.Cell().Background(Colors.Blue.Lighten4).Padding(5).Text("Count").Bold().AlignRight().FontSize(9);
                                header.Cell().Background(Colors.Blue.Lighten4).Padding(5).Text("Deposits").Bold().AlignRight().FontSize(9);
                                header.Cell().Background(Colors.Blue.Lighten4).Padding(5).Text("Withdrawals").Bold().AlignRight().FontSize(9);
                                header.Cell().Background(Colors.Blue.Lighten4).Padding(5).Text("Net").Bold().AlignRight().FontSize(9);
                                header.Cell().Background(Colors.Blue.Lighten4).Padding(5).Text("Closing Balance").Bold().AlignRight().FontSize(9);
                            });
                            
                            foreach (var month in report.MonthlyReports)
                            {
                                table.Cell().Padding(3).Text($"{month.MonthName} {month.Year}");
                                table.Cell().Padding(3).Text($"{month.TransactionCount}").AlignRight();
                                table.Cell().Padding(3).Text($"{month.Deposits:N0}").AlignRight();
                                table.Cell().Padding(3).Text($"{month.Withdrawals:N0}").AlignRight();
                                table.Cell().Padding(3).Text($"{month.NetCashFlow:N0}").AlignRight()
                                    .FontColor(month.NetCashFlow >= 0 ? Colors.Green.Darken2 : Colors.Red.Darken2);
                                table.Cell().Padding(3).Text($"{month.ClosingBalance:N0}").AlignRight();
                            }
                        });
                    });
                }
                
                void ComposeUserActivity(IContainer innerContainer)
                {
                    innerContainer.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(col =>
                    {
                        col.Spacing(8);
                        col.Item().Text("👥 USER ACTIVITY").FontSize(14).Bold().FontColor(Colors.Blue.Darken2);
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                            });
                            
                            table.Header(header =>
                            {
                                header.Cell().Background(Colors.Blue.Lighten4).Padding(5).Text("User").Bold().FontSize(9);
                                header.Cell().Background(Colors.Blue.Lighten4).Padding(5).Text("Count").Bold().AlignRight().FontSize(9);
                                header.Cell().Background(Colors.Blue.Lighten4).Padding(5).Text("Deposits").Bold().AlignRight().FontSize(9);
                                header.Cell().Background(Colors.Blue.Lighten4).Padding(5).Text("Withdrawals").Bold().AlignRight().FontSize(9);
                                header.Cell().Background(Colors.Blue.Lighten4).Padding(5).Text("Net").Bold().AlignRight().FontSize(9);
                                header.Cell().Background(Colors.Blue.Lighten4).Padding(5).Text("Avg/Transaction").Bold().AlignRight().FontSize(9);
                            });
                            
                            foreach (var user in report.UserReports)
                            {
                                var avgPerTransaction = user.TransactionCount > 0 
                                    ? (user.Deposits + user.Withdrawals) / user.TransactionCount 
                                    : 0;
                                
                                table.Cell().Padding(3).Text(user.UserName);
                                table.Cell().Padding(3).Text($"{user.TransactionCount}").AlignRight();
                                table.Cell().Padding(3).Text($"{user.Deposits:N0}").AlignRight();
                                table.Cell().Padding(3).Text($"{user.Withdrawals:N0}").AlignRight();
                                table.Cell().Padding(3).Text($"{user.NetCashFlow:N0}").AlignRight()
                                    .FontColor(user.NetCashFlow >= 0 ? Colors.Green.Darken2 : Colors.Red.Darken2);
                                table.Cell().Padding(3).Text($"{avgPerTransaction:N0}").AlignRight();
                            }
                        });
                    });
                }
                
                void ComposeTransactionDetails(IContainer innerContainer)
                {
                    innerContainer.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(col =>
                    {
                        col.Spacing(8);
                        col.Item().Text("📋 TRANSACTION DETAILS").FontSize(14).Bold().FontColor(Colors.Blue.Darken2);
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn();
                                columns.RelativeColumn(0.5f);
                                columns.RelativeColumn(0.8f);
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                            });
                            
                            table.Header(header =>
                            {
                                header.Cell().Background(Colors.Blue.Lighten4).Padding(5).Text("Date").Bold().FontSize(8);
                                header.Cell().Background(Colors.Blue.Lighten4).Padding(5).Text("Time").Bold().FontSize(8);
                                header.Cell().Background(Colors.Blue.Lighten4).Padding(5).Text("Consumer").Bold().FontSize(8);
                                header.Cell().Background(Colors.Blue.Lighten4).Padding(5).Text("Category").Bold().FontSize(8);
                                header.Cell().Background(Colors.Blue.Lighten4).Padding(5).Text("Title").Bold().FontSize(8);
                                header.Cell().Background(Colors.Blue.Lighten4).Padding(5).Text("Withdraw").Bold().AlignRight().FontSize(8);
                                header.Cell().Background(Colors.Blue.Lighten4).Padding(5).Text("Deposit").Bold().AlignRight().FontSize(8);
                            });
                            
                            var rowIndex = 0;
                            foreach (var transaction in report.Transactions)
                            {
                                var categoryName = report.CategoryReports.FirstOrDefault(c => c.CategoryId == transaction.CategoryId)?.CategoryName ?? "Unknown";
                                var consumerName = report.UserReports.FirstOrDefault(u => u.UserId == transaction.ConsumerId)?.UserName ?? "Unknown User";
                                var backgroundColor = rowIndex++ % 2 == 0 ? Colors.Grey.Lighten4 : Colors.White;

                                table.Cell().Padding(3).Background(backgroundColor).Text($"{MainService.ConvertGregorianToJalali(transaction.Date)}").FontSize(8);
                                table.Cell().Padding(3).Background(backgroundColor).Text($"{transaction.Date:HH:mm}").FontSize(8);
                                table.Cell().Padding(3).Background(backgroundColor).Text(consumerName).FontSize(8);
                                table.Cell().Padding(3).Background(backgroundColor).Text(categoryName).FontSize(8);
                                table.Cell().Padding(3).Background(backgroundColor).Text(MainService.TruncateString(transaction.Title, 20)).FontSize(8);
                                table.Cell().Padding(3).Background(backgroundColor).Text(transaction.Withdraw > 0 ? $"{transaction.Withdraw:N0}" : "").AlignRight().FontSize(8);
                                table.Cell().Padding(3).Background(backgroundColor).Text(transaction.Deposit > 0 ? $"{transaction.Deposit:N0}" : "").AlignRight().FontSize(8);
                            }
                        });
                    });
                }
                
                void ComposeFooter(IContainer innerContainer)
                {
                    innerContainer.AlignCenter().Column(col =>
                    {
                        col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
                        col.Spacing(3);
                        col.Item().Text(x =>
                        {
                            x.Span("Page ");
                            x.CurrentPageNumber();
                            x.Span(" of ");
                            x.TotalPages();
                        });
                    });
                }
            });
        });
        
        return document.GeneratePdf();
    }
    
    public byte[] GenerateExcel(WalletReport report)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Transactions");
        var headers = new[] { "Date", "Time", "Consumer", "Category", "Title", "Withdraw", "Deposit" };
        for (var i = 0; i < headers.Length; i++)
        {
            worksheet.Cell(1, i + 1).Value = headers[i];
            worksheet.Cell(1, i + 1).Style.Font.Bold = true;
            worksheet.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.LightBlue;
        }
        
        var row = 2;
        foreach (var transaction in report.Transactions)
        {
            var categoryName = report.CategoryReports.FirstOrDefault(c => c.CategoryId == transaction.CategoryId)?.CategoryName ?? "Unknown";
            var consumerName = report.UserReports.FirstOrDefault(u => u.UserId == transaction.ConsumerId)?.UserName ?? "Unknown User";
            
            worksheet.Cell(row, 1).Value = MainService.ConvertGregorianToJalali(transaction.Date);
            worksheet.Cell(row, 2).Value = transaction.Date.ToString("HH:mm");
            worksheet.Cell(row, 3).Value = consumerName;
            worksheet.Cell(row, 4).Value = categoryName;
            worksheet.Cell(row, 5).Value = transaction.Title ?? "";
            worksheet.Cell(row, 6).Value = transaction.Withdraw > 0 ? transaction.Withdraw : 0;
            worksheet.Cell(row, 7).Value = transaction.Deposit > 0 ? transaction.Deposit : 0;
            row++;
        }
        
        worksheet.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
