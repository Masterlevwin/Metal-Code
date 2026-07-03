using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Metal_Code.Models
{
    // ==================== TypeDetail ====================
    public class TypeDetail
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public float Price { get; set; }
        public string? Sort { get; set; }
        public TypeDetail() { }
    }

    // ==================== Work ====================
    public class Work
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public float Price { get; set; }
        public float Time { get; set; }
        public Work() { }
    }

    // ==================== Manager ====================
    public class Manager
    {
        public int Id { get; set; }
        [Required]
        public string? Name { get; set; }
        public string? MachineName { get; set; }
        public string? Contact { get; set; }
        public string? Password { get; set; }
        public bool IsAdmin { get; set; }
        public bool IsEngineer { get; set; }
        public bool IsLaser { get; set; }

        [NotMapped]
        public ObservableCollection<Offer> Offers { get; set; } = new();

        [NotMapped]
        public ObservableCollection<Customer> Customers { get; set; } = new();

        public Manager() { }
    }

    // ==================== Offer ====================
    public class Offer : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public Offer() { }

        public Offer(string? n = null, string? company = null, float amount = 0, float material = 0, float services = 0)
        {
            N = n;
            Company = company;
            Amount = amount;
            Material = material;
            Services = services;
            CreatedDate = DateTime.UtcNow;
        }

        [Browsable(false)]
        public int Id { get; set; }

        public string? N { get; set; }
        public string? Company { get; set; }
        public float Amount { get; set; }
        public float Material { get; set; }

        [ConcurrencyCheck]
        public bool Agent { get; set; }

        [ConcurrencyCheck]
        public string? Invoice { get; set; }

        [ConcurrencyCheck]
        public DateTime? CreatedDate { get; set; }

        [ConcurrencyCheck]
        public string? Order { get; set; }

        public string? Autor { get; set; }

        [ConcurrencyCheck]
        public DateTime? EndDate { get; set; }

        [Browsable(false)]
        public float Services { get; set; }

        [Browsable(false)]
        public string? Act { get; set; }

        [Browsable(false)]
        public int ManagerId { get; set; }

        [Browsable(false)]
        public Manager? Manager { get; set; }

        [Browsable(false)]
        public string? Data { get; set; }

        [Browsable(false)]
        public bool IsPendingSync { get; set; } = true;

        [NotMapped]
        [Browsable(false)]
        public string ParentQuoteNumber
        {
            get
            {
                if (string.IsNullOrWhiteSpace(N)) return "Без номера";
                var match = Regex.Match(N.Trim(), @"^(\d+)");
                return match.Success ? match.Groups[1].Value : N.Trim();
            }
        }

        private bool _isLocalOffer;
        [NotMapped]
        public bool IsLocalOffer
        {
            get => _isLocalOffer;
            set
            {
                if (_isLocalOffer != value)
                {
                    _isLocalOffer = value;
                    OnPropertyChanged();
                }
            }
        }
    }

    // ==================== Customer ====================
    public class Customer
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Address { get; set; }
        public bool Agent { get; set; }
        public int DeliveryPrice { get; set; }

        public int ManagerId { get; set; }

        public string? SpecTemplateJson { get; set; }

        public Manager? Manager { get; set; }

        public SpecTemplate SpecTemplate { get; set; } = new SpecTemplate();

        public Customer() { }
    }

    // ==================== Metal ====================
    public class Metal
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public float Density { get; set; }
        public float MassPrice { get; set; }

        [Browsable(false)]
        public string? WayPrice { get; set; }
        [Browsable(false)]
        public string? PinholePrice { get; set; }
        [Browsable(false)]
        public string? MoldPrice { get; set; }

        public Metal() { }
    }

    // ==================== RequestTemplate ====================
    public class RequestTemplate : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = "") =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public int Id { get; set; }

        private string _name = "по умолчанию";
        public string Name
        {
            get => _name;
            set { if (_name != value) { _name = value; OnPropertyChanged(); } }
        }

        private string _destinyPattern = "s";
        public string DestinyPattern
        {
            get => _destinyPattern;
            set { if (_destinyPattern != value) { _destinyPattern = value; OnPropertyChanged(); } }
        }

        private string _countPattern = "n";
        public string CountPattern
        {
            get => _countPattern;
            set { if (_countPattern != value) { _countPattern = value; OnPropertyChanged(); } }
        }

        private bool _posDestiny = true;
        public bool PosDestiny
        {
            get => _posDestiny;
            set { if (_posDestiny != value) { _posDestiny = value; OnPropertyChanged(); } }
        }

        private bool _posCount = true;
        public bool PosCount
        {
            get => _posCount;
            set { if (_posCount != value) { _posCount = value; OnPropertyChanged(); } }
        }

        public RequestTemplate() { }
    }

    // ==================== UpdateItem ====================
    public class UpdateItem
    {
        public int Id { get; set; }
        public string? VersionTitle { get; set; }
        public string? Description { get; set; }
        public string? ScreenshotPath { get; set; }
        public DateTime ReleaseDate { get; set; }
        public bool IsShownAtStartup { get; set; }
        public UpdateItem() { }
    }

    // ==================== SpecTemplate ====================
    public class SpecTemplate : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string prop = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        private string header = "Приложение № 1 к договору поставки №";
        public string Header
        {
            get => header;
            set { if (header != value) { header = value; OnPropertyChanged(nameof(Header)); } }
        }

        private int number = 1;
        public int Number
        {
            get => number;
            set { if (number != value) { number = value; OnPropertyChanged(nameof(Number)); } }
        }

        private string terms = "100% предоплата.";
        public string Terms
        {
            get => terms;
            set { if (terms != value) { terms = value; OnPropertyChanged(nameof(Terms)); } }
        }

        private string provider = "ООО ЛАЗЕРФЛЕКС";
        public string Provider
        {
            get => provider;
            set { if (provider != value) { provider = value; OnPropertyChanged(nameof(Provider)); } }
        }

        private string buyer = string.Empty;
        public string Buyer
        {
            get => buyer;
            set { if (buyer != value) { buyer = value; OnPropertyChanged(nameof(Buyer)); } }
        }

        public SpecTemplate() { }
    }
}