using System;
using System.Collections.Generic;

namespace Metal_Code
{
    public class ReportOfferItem
    {
        public DateTime? CreatedDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? Invoice { get; set; }
        public string? Company { get; set; }
        public string? Order { get; set; }
        public string? N { get; set; }
        public bool IsAgent { get; set; }

        // "Грязные" суммы (как в исходных данных, для отображения)
        public decimal Services { get; set; }
        public decimal Material { get; set; }
        public decimal Amount { get; set; }

        // Чистые суммы (после вычета бонуса — для расчёта прибыли)
        public decimal ServicesNet { get; set; }
        public decimal MaterialNet { get; set; }

        public decimal BonusRatio { get; set; }
        public decimal BonusAmount { get; set; }
        public bool IsNoBonus { get; set; }
    }

    public class ReportResult
    {
        public List<ReportOfferItem> OooItems { get; set; } = new();
        public List<ReportOfferItem> IpItems { get; set; } = new();

        // Суммы для расчёта прибыли — ТОЛЬКО ЧИСТЫЕ (без бонусов)
        public decimal TotalServicesOoo { get; set; }
        public decimal TotalMaterialOoo { get; set; }
        public decimal TotalServicesIp { get; set; }
        public decimal TotalMaterialIp { get; set; }

        // Бонусы — отдельно
        public decimal TotalBonusOoo { get; set; }
        public decimal TotalBonusIp { get; set; }
        public decimal NoBonusAmount { get; set; }

        // Итоги
        public decimal TotalAmountOoo { get; set; }   // общая сумма расчетов ООО
        public decimal TotalAmountIp { get; set; }    // общая сумма расчетов ИП
        public decimal CleanProfit { get; set; }      // прибыль без бонусов
        public decimal Plan { get; set; }             // CleanProfit + бонусы
        public decimal BonusOoo { get; set; }         // сверхплановый бонус ООО
        public decimal BonusIp { get; set; }          // бонус ИП
        public decimal TotalSalary { get; set; }      // итоговая зарплата
    }
}
