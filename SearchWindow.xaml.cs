using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для SearchWindow.xaml
    /// </summary>
    public partial class SearchWindow : Window
    {
        public string Search { get; set; } = "";

        public SearchWindow() { InitializeComponent(); DataContext = this; }

        private void Get_Offers(object sender, RoutedEventArgs e) 
        {
            if (Search == "") MainWindow.M.CreateWorker(GetOffers_WithoutMainBase, MainWindow.ActionState.get);
            else MainWindow.M.CreateWorker(Get_Offers, MainWindow.ActionState.get);
            Close();
        }

        //метод запуска процесса загрузки отфильтрованных расчетов из основной базы в локальную
        private string Get_Offers(string? message = null)
        {
            int count = 0;

            using ManagerContext db = new(MainWindow.M.connections[1]);      //подключаемся к основной базе данных
            bool isAvalaible = db.Database.CanConnect();        //проверяем, свободна ли база для подключения
            if (isAvalaible)
            {
                try
                {
                    List<Offer>? offers = db.Offers.Where(o => o.ManagerId == MainWindow.M.TargetManager.Id && (o.N == Search || o.Company == Search)).ToList();

                    if (offers.Count == 0) return "Расчетов по выбранным параметрам не найдено";
                    else
                    {
                        //подключаемся к локальной базе данных
                        using ManagerContext dbLocal = new(MainWindow.M.connections[0]);

                        //ищем менеджера в локальной базе по имени соответствующего локальному, при этом загружаем его расчеты
                        Manager? _manLocal = dbLocal.Managers.Where(m => m.Name == MainWindow.M.TargetManager.Name).Include(c => c.Offers).FirstOrDefault();

                        foreach (Offer offer in offers)
                        {
                            //проверяем наличие идентичного КП в локальной базе, и если такое уже есть, пропускаем копирование
                            Offer? tempOffer = _manLocal?.Offers.Where(o => o.N == offer.N
                                                                && o.Company == offer.Company
                                                                && o.Amount == offer.Amount).FirstOrDefault();
                            if (tempOffer != null) continue;

                            //копируем итеративное КП в новое с целью автоматического присваивания Id при вставке в базу
                            Offer _offer = new(offer.N, offer.Company, offer.Amount, offer.Material, offer.Services)
                            {
                                Agent = offer.Agent,
                                Invoice = offer.Invoice,
                                Order = offer.Order,
                                Act = offer.Act,
                                CreatedDate = offer.CreatedDate,
                                EndDate = offer.EndDate,
                                Autor = offer.Autor,
                                Manager = _manLocal,        //указываем соответствующего менеджера  
                                Data = offer.Data
                            };

                            _manLocal?.Offers.Add(_offer);  //переносим расчет в базу этого менеджера
                            count++;
                        }
                        dbLocal.SaveChanges();                  //сохраняем изменения в локальной базе данных
                    }
                }
                catch (DbUpdateConcurrencyException ex) { return ex.Message; }
            }

            return $"Локальная база обновлена. Добавлено {count} расчетов.";
        }

        //метод запуска процесса загрузки ВСЕХ расчетов из основной базы в локальную
        private string GetOffers_WithoutMainBase(string? message = null)
        {
            if (!MainWindow.M.IsLocal) return "Загружена основная база расчетов. Обновление не требуется.";

            int count = 0;

            using ManagerContext db = new(MainWindow.M.connections[1]);      //подключаемся к основной базе данных
            bool isAvalaible = db.Database.CanConnect();        //проверяем, свободна ли база для подключения
            if (isAvalaible)
            {
                try
                {
                    //подключаемся к локальной базе данных
                    using ManagerContext dbLocal = new(MainWindow.M.connections[0]);

                    //ищем менеджера в основной базе по имени соответствующего выбранному, при этом загружаем его расчеты
                    Manager? _man = db.Managers.Where(m => m.Name == MainWindow.M.TargetManager.Name).Include(c => c.Offers).FirstOrDefault();

                    //ищем менеджера в локальной базе по имени соответствующего локальному, при этом загружаем его расчеты
                    Manager? _manLocal = dbLocal.Managers.Where(m => m.Name == MainWindow.M.TargetManager.Name).Include(c => c.Offers).FirstOrDefault();

                    if (_man?.Offers.Count > 0)
                        foreach (Offer offer in _man.Offers)
                        {
                            //проверяем наличие идентичного КП в локальной базе, и если такое уже есть, пропускаем копирование
                            Offer? tempOffer = _manLocal?.Offers.Where(o => o.N == offer.N
                                                                && o.Company == offer.Company
                                                                && o.Amount == offer.Amount).FirstOrDefault();
                            if (tempOffer != null) continue;

                            //копируем итеративное КП в новое с целью автоматического присваивания Id при вставке в базу
                            Offer _offer = new(offer.N, offer.Company, offer.Amount, offer.Material, offer.Services)
                            {
                                Agent = offer.Agent,
                                Invoice = offer.Invoice,
                                Order = offer.Order,
                                Act = offer.Act,
                                CreatedDate = offer.CreatedDate,
                                EndDate = offer.EndDate,
                                Autor = offer.Autor,
                                Manager = _manLocal,        //указываем соответствующего менеджера  
                                Data = offer.Data
                            };

                            _manLocal?.Offers.Add(_offer);  //переносим расчет в базу этого менеджера
                            count++;
                        }
                    dbLocal.SaveChanges();                  //сохраняем изменения в локальной базе данных
                }
                catch (DbUpdateConcurrencyException ex) { return ex.Message; }
            }
            return $"Локальная база обновлена. Добавлено {count} расчетов.";
        }
    }
}
