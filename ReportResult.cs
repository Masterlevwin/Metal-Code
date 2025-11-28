using System;
using System.Collections.Generic;

namespace Metal_Code
{
    public class ReportOfferItem
    {
        public DateTime? CreatedDate { get; set; }
        public string? N { get; set; }
        public string? Company { get; set; }
        public string? Invoice { get; set; }
        public string? Order { get; set; }
        public decimal Services { get; set; }
        public decimal Material { get; set; }
        public decimal Amount { get; set; }
        public bool IsAgent { get; set; }
        public decimal BonusRatio { get; set; }
        public decimal BonusAmount { get; set; }
        public bool IsNoBonus { get; set; } // содержит "без бонуса"
    }

    public class ReportResult
    {
        // Детали
        public List<ReportOfferItem> OooItems { get; set; } = new();
        public List<ReportOfferItem> IpItems { get; set; } = new();

        // Итоги
        public decimal Plan { get; set; }
        public decimal BonusOoo { get; set; }
        public decimal BonusIp { get; set; }
        public decimal TotalSalary { get; set; }

        // Промежуточные суммы (для Excel)
        public decimal TotalServicesOoo { get; set; }
        public decimal TotalMaterialOoo { get; set; }
        public decimal TotalServicesIp { get; set; }
        public decimal TotalMaterialIp { get; set; }
        public decimal TotalBonusOoo { get; set; }
        public decimal TotalBonusIp { get; set; }
        public decimal NoBonusAmount { get; set; } // сумма Amount по "без бонуса"
    }
}
