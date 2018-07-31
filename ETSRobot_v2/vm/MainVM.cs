using altaik.baseapp.vm;
using altaik.baseapp.vm.command;
using ETSRobot_v2.model;
using ETSRobot_v2.model.service;
using ETSRobot_v2.service;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace ETSRobot_v2.vm {
    public class MainVM : BaseVM {
        #region Variables
        private DbETSManager dbETSManager;
        private ObservableCollection<Order> orderData;
        private ObservableCollection<AutoOrder> autoOrderData;
        private DataManager dataMananger;
        private int modeType = 2;
        private Broker curBroker = new Broker();
        private DispatcherTimer dTimer = new DispatcherTimer();
        private DataBaseManager dataBaseManager;
        #endregion

        #region Methods
        public MainVM() {
            AppJournal.StartProc();
            AppJournal.Write("MainVM", "Start programm");

            Init();
        }

        private void Init() {
            AppJournal.Write("MainVM", "Set default parametrs");

            dbETSManager = new DbETSManager();

            orderData = new ObservableCollection<Order>();
            autoOrderData = new ObservableCollection<AutoOrder>();
            dataMananger = new DataManager();

            ModeFirstCmdChk = false;
            ModeSimpleCmdChk = true;
            AddCmdEnable = false;
            DeleteCmdEnable = false;
            AutoOrdersVisible = Visibility.Hidden;
            ConnectEnableBtn = true;
            OrdersVisible = Visibility.Visible;
            TableTitleTxt = "Таблица лотов и их статус в обычном режиме";

            CheckCurBroker();

            dTimer.Tick += new EventHandler(dTimerTick);
            dTimer.Interval = new TimeSpan(0, 0, 1);
            dTimer.Start();
        }


        private void CheckCurBroker() {
            AppJournal.Write("MainVM", "Check for current broker");

            Process[] procs = Process.GetProcesses();

            String bName = "";

            foreach (var item in procs) {
                if (item.MainWindowTitle.Contains("ETS Plaza Workstation")) {
                    bName = item.MainWindowTitle.Substring(item.MainWindowTitle.IndexOf("[") + 1, 5);
                    bName = bName.Trim(' ');
                }
            }

            SetBroker(bName.ToUpper());
        }


        private void dTimerTick(object sender, EventArgs e) { CTime = DateTime.Now.ToLongTimeString(); }


        public ICommand ModeFirstCmd { get { return new DelegateCommand(() => ModeSwitcher(1)); } }
        public ICommand ModeSimpleCmd { get { return new DelegateCommand(() => ModeSwitcher(2)); } }
        private void ModeSwitcher(int mode) {
            modeType = mode;

            ModeFirstCmdChk = mode == 1 ? true : false;
            ModeSimpleCmdChk = mode == 1 ? false : true;

            AddCmdEnable = mode == 1 ? true : false;
            DeleteCmdEnable = mode == 1 ? true : false;
            AutoOrdersVisible = mode == 1 ? Visibility.Visible : Visibility.Hidden;
            OrdersVisible = mode == 1 ? Visibility.Hidden : Visibility.Visible;

            TableTitleTxt = "Таблица лотов и их статус в [обычном режиме]";
        }


        public ICommand AddCmd { get { return new DelegateCommand(AddOrder); } }
        private void AddOrder() {
            AppJournal.Write("MainVM", "Add order record");

            if (modeType == 2) {
                orderData.Add(new Order {
                    Number = (orderData.Count + 1),
                    FirmBroker = dataMananger.firmData,
                    SelectedFirmBroker = dataMananger.firmData.FirstOrDefault(b => b.id == curBroker.id),
                    Client = dataMananger.clientData,
                    Issue = dataMananger.issueData,
                    BottomPrice = 0,
                    Step = 10,
                    LastPrice = 0,
                    CurrentPrice = 0,
                    ModeType = dataMananger.modeData,
                    Place = 0,
                    StartCmdName = "Запуск",
                    IsPrimary = true,
                    PauseTime = 3000
                });

                OrdersVM orderVM = new OrdersVM();
                orderVM.Orders = orderData;
                OrdersVM = orderVM;
            } else if (modeType == 1) {
                autoOrderData.Add(new AutoOrder { Number = (autoOrderData.Count + 1) });

                AutoOrdersVM autoOrderVM = new AutoOrdersVM();

                autoOrderVM.AutoOrders = autoOrderData;
                AutoOrdersVM = autoOrderVM;
            }

            ServerLogsTxt += "Заявка добавлена\n";
        }


        public ICommand DeleteCmd { get { return new DelegateCommand(DeleteOrder); } }
        private void DeleteOrder() {
            AppJournal.Write("MainVM", "Delete order record");

            if (modeType == 2) orderData.Remove(OrdersVM.SelectedOrder);
            else if (modeType == 1) autoOrderData.Remove(AutoOrdersVM.SelectedAutoOrder);

            ServerLogsTxt += "Заявка удалена\n";
        }


        public ICommand ConnectCmd { get { return new DelegateCommand(Connect); } }
        private void Connect() {
            AppJournal.Write("MainVM", "Start connecting to ETS");

            dataMananger.MakeModes();

            if (dbETSManager.Connected(curBroker.name)) {
                ServerLogsTxt += "Соединение с сервером установлено\n";

                if (dataMananger.FillIssueData()) ServerLogsTxt += "Данные инструментов (лотов) загружены\n";
                else ServerLogsTxt += "Данные инструментов (лотов) не загружены. Проверьте соединение!\n";

                if (dataMananger.FillFirmBrokerData()) ServerLogsTxt += "Данные фирм брокеров загружены\n";
                else ServerLogsTxt += "Данные фирм брокеров не загружены. Проверьте соединение!\n";

                if (dataMananger.FillClientData()) {
                    ServerLogsTxt += "Данные клиентов загружены\n";
                    AddCmdEnable = true;
                    DeleteCmdEnable = true;
                } else ServerLogsTxt += "Данные клиентов не загружены. Проверьте соединение!\n";

                ConnectEnableBtn = false;
            } else ServerLogsTxt = "Соединение с сервером не установлено\n";
        }


        private void InitConnect() {
            if (dbETSManager.Connected(curBroker.name)) {
                ServerLogsTxt += "Начальное Соединение с сервером установлено\n";
            } else
                ServerLogsTxt = "Начальное Соединение с сервером не установлено\n";
        }


        RestrictLoadedDataManager restrictedDB;

        private void AutoConnect(List<ProcessedLot> plots) {
            InitConnect();

            restrictedDB = new RestrictLoadedDataManager(dataMananger);
            restrictedDB.SetForIssue(plots);

            if (restrictedDB.FillIssueData()) ServerLogsTxt += "Данные инструментов (лотов) загружены\n";
            else ServerLogsTxt += "Данные инструментов (лотов) не загружены. Проверьте соединение!\n";
        }


        public ICommand AppCloseCmd { get { return new DelegateCommand(CloseApp); } }
        private void CloseApp() {
            dbETSManager.Close();
            Environment.Exit(0);
        }


        public ICommand AutoSendCmd { get { return new DelegateCommand(AutoSend); } }
        private void AutoSend() {
            List<ProcessedLot> pLots = new List<ProcessedLot>();

            if (modeType == 1) {
                if (AutoOrdersVM.AutoOrders.Count != 0) {
                    foreach (var autoOrder in AutoOrdersVM.AutoOrders) {
                        pLots.Add(new ProcessedLot {
                            lotNo = autoOrder.LotName.ToUpper(),
                            startPrice = autoOrder.Nominal,
                            brokerId = autoOrder.SelectedBroker.id,
                            clientCode = autoOrder.ClientCode,
                            percent = autoOrder.Procent
                        });
                    }
                }
            }

            AutoConnect(pLots);

            Task timerTask = new Task(() => LotsPass(pLots));
            timerTask.Start();
        }


        private bool isBusy;
        private int statusId = 0;

        private void LotsPass(List<ProcessedLot> plots) {
            DSSERVERLib.GMsgQuoteS[] msgQuotes = new DSSERVERLib.GMsgQuoteS[plots.Count];
            Task[] timerTask = new Task[plots.Count];
            Task[] baseTask = new Task[4];

            int iCount = 0, iTask = 0;

            foreach (var plot in plots) {
                isBusy = true;

                decimal tmpPrice = 0;

                statusId = 0;

                if (modeType == 1) tmpPrice = GetPrice(plot.startPrice, plot.percent);

                msgQuotes[iCount] = GetNewMsg(plot.lotNo, tmpPrice.ToString(), plot.clientCode, plot.brokerId);

                if (modeType == 1) timerTask[iCount] = new Task(() => AutoSendTask(msgQuotes[iCount]));

                timerTask[iCount].Start();

                while (isBusy) ;

                iCount++;
            }
        }


        private decimal GetPrice(decimal price, int percent) { return Math.Round(price - (price / Convert.ToDecimal(100) * Convert.ToDecimal(percent) - Convert.ToDecimal(1))); }


        private DSSERVERLib.GMsgQuoteS GetNewMsg(String lotNo, String price, String clientCode, int brokerId) {
            DSSERVERLib.GMsgQuoteS msg = new DSSERVERLib.GMsgQuoteS();

            msg.Msg_action = 78;
            msg.Id = 0;
            msg.type = 65;
            msg.IssueID = restrictedDB.GetIssueId(lotNo);
            msg.Issue_name = lotNo;
            msg.Type_wks = 1;
            msg.Price = price;
            msg.Qty = 1;
            msg.Paycond = 84;
            msg.Dcc = "";
            msg.Delivery_days = 10;
            msg.Settl_pair = clientCode;
            msg.Mm = 0;
            msg.Leave = 1;
            msg.E_s = 0;
            msg.FirmID = brokerId;

            return msg;
        }


        private string info = "";

        private void AutoSendTask0(DSSERVERLib.GMsgQuoteS msg) { while (!info.Contains("has been added")) msg.Send(out info); }


        private void AutoSendTask(DSSERVERLib.GMsgQuoteS msg, String tWhen = "", decimal price = 0) {
            if (modeType == 1) {
                while (!info.Contains("has been added")) {
                    msg.Send(out info);
                    isBusy = false;
                }
            } else if (modeType == 3) {
                while (isBusy) {
                    if (Convert.ToInt32(tWhen.Substring(0, 2)) <= Convert.ToInt32(DateTime.Now.Hour)) {
                        if (Convert.ToInt32(tWhen.Substring(3, 2)) <= Convert.ToInt32(DateTime.Now.Minute)) {
                            if (Convert.ToInt32(tWhen.Substring(6, 2)) <= Convert.ToInt32(DateTime.Now.Second)) {
                                msg.Send(out info);
                                ServerLogsTxt += "\n" + tWhen + " - " + info;

                                if (info.Contains("has been added")) statusId = 1;
                                else statusId = 2;

                                isBusy = false;
                            }
                        }
                    }
                }
            }
        }


        public ICommand AltaCmd { get { return new DelegateCommand(() => SetBroker("ALTA")); } }
        public ICommand KordCmd { get { return new DelegateCommand(() => SetBroker("KORD")); } }
        public ICommand AkalCmd { get { return new DelegateCommand(() => SetBroker("AKAL")); } }
        public ICommand AltkCmd { get { return new DelegateCommand(() => SetBroker("ALTK")); } }
        public ICommand Trn6Cmd { get { return new DelegateCommand(() => SetBroker("TRN6")); } }
        public ICommand Trn7Cmd { get { return new DelegateCommand(() => SetBroker("TRN7")); } }
        public ICommand Trn8Cmd { get { return new DelegateCommand(() => SetBroker("TRN8")); } }
        public ICommand Trn9Cmd { get { return new DelegateCommand(() => SetBroker("TRN9")); } }
        public ICommand Trn10Cmd { get { return new DelegateCommand(() => SetBroker("TRN10")); } }
        public ICommand Trn11Cmd { get { return new DelegateCommand(() => SetBroker("TRN11")); } }
        private void SetBroker(String broker) {
            AppJournal.Write("MainVM", "Set broker");

            AltaCmdChk = false;
            KordCmdChk = false;
            AkalCmdChk = false;
            AltkCmdChk = false;
            Trn6CmdChk = false;
            Trn7CmdChk = false;
            Trn8CmdChk = false;
            Trn9CmdChk = false;
            Trn10CmdChk = false;
            Trn11CmdChk = false;

            switch (broker) {
                case "ALTA": AltaCmdChk = true; curBroker.id = 430; curBroker.name = broker; CurBroker = "Альтаир Нур"; break;
                case "KORD": KordCmdChk = true; curBroker.id = 443; curBroker.name = broker; CurBroker = "Корунд-777"; break;
                case "AKAL": AkalCmdChk = true; curBroker.id = 470; curBroker.name = broker; CurBroker = "Ак Алтын Ко"; break;
                case "ALTK": AltkCmdChk = true; curBroker.id = 455; curBroker.name = broker; CurBroker = "Альта и К"; break;
                case "TRN6K": Trn8CmdChk = true; curBroker.id = 447; curBroker.name = broker; CurBroker = "Тестовый TRN6"; break;
                case "TRN7K": Trn8CmdChk = true; curBroker.id = 447; curBroker.name = broker; CurBroker = "Тестовый TRN7"; break;
                case "TRN8K": Trn8CmdChk = true; curBroker.id = 447; curBroker.name = broker; CurBroker = "Тестовый TRN8"; break;
                case "TRN9K": Trn9CmdChk = true; curBroker.id = 448; curBroker.name = broker; CurBroker = "Тестовый TRN9"; break;
                case "TRN10": Trn10CmdChk = true; curBroker.id = 449; curBroker.name = broker; CurBroker = "Тестовый TRN10"; break;
                case "TRN11": Trn11CmdChk = true; curBroker.id = 452; curBroker.name = broker; CurBroker = "Тестовый TRN11"; break;
            }
        }


        public ICommand SendAllCmd => new DelegateCommand(() => SendAll(true));
        public ICommand StopAllCmd => new DelegateCommand(() => SendAll(false));
        private void SendAll(bool type) {
            foreach (var item in orderData) {
                item.StartCmdName = type ? "Запуск" : "Стоп";
                item.StartOrder();
            }
        }


        public ICommand SyncTimeCmd => new DelegateCommand(SyncTime);
        private void SyncTime() {
            try {
                ServerTime = string.Format("{0}, разница {1}", DateTime.Now.ToLongTimeString(), dbETSManager.GetTimeDelta().ToString());
            } catch { ServerTime = "Try again"; }
        }
        #endregion

        #region Bindings
        private OrdersVM ordersVM;
        public OrdersVM OrdersVM {
            get { return ordersVM; }
            set { ordersVM = value; RaisePropertyChangedEvent("OrdersVM"); }
        }


        private AutoOrdersVM autoOrdersVM;
        public AutoOrdersVM AutoOrdersVM {
            get { return autoOrdersVM; }
            set { autoOrdersVM = value; RaisePropertyChangedEvent("AutoOrdersVM"); }
        }


        public String Title { get { return "Автоматизированная система подачи заявок на ETS"; } }


        private String tableTitleTxt;
        public String TableTitleTxt {
            get { return tableTitleTxt; }
            set { tableTitleTxt = value; RaisePropertyChangedEvent("TableTitleTxt"); }
        }


        private Visibility ordersVisible;
        public Visibility OrdersVisible {
            get { return ordersVisible; }
            set { ordersVisible = value; RaisePropertyChangedEvent("OrdersVisible"); }
        }


        private Visibility autoOrdersVisible;
        public Visibility AutoOrdersVisible {
            get { return autoOrdersVisible; }
            set { autoOrdersVisible = value; RaisePropertyChangedEvent("AutoOrdersVisible"); }
        }


        private String serverLogsTxt;
        public String ServerLogsTxt {
            get { return serverLogsTxt; }
            set { serverLogsTxt = value; RaisePropertyChangedEvent("ServerLogsTxt"); }
        }


        private String cBroker;
        public String CurBroker {
            get { return cBroker; }
            set { cBroker = value; RaisePropertyChangedEvent("CurBroker"); }
        }


        private String cTime;
        public String CTime {
            get { return cTime; }
            set { cTime = value; RaisePropertyChangedEvent("CTime"); }
        }


        private Boolean modeFirstCmdChk;
        public Boolean ModeFirstCmdChk {
            get { return modeFirstCmdChk; }
            set { modeFirstCmdChk = value; RaisePropertyChangedEvent("ModeFirstCmdChk"); }
        }


        private Boolean altaCmdChk;
        public Boolean AltaCmdChk { get { return altaCmdChk; } set { altaCmdChk = value; RaisePropertyChangedEvent("AltaCmdChk"); } }

        private Boolean kordCmdChk;
        public Boolean KordCmdChk { get { return kordCmdChk; } set { kordCmdChk = value; RaisePropertyChangedEvent("KordCmdChk"); } }

        private Boolean akalCmdChk;
        public Boolean AkalCmdChk { get { return akalCmdChk; } set { akalCmdChk = value; RaisePropertyChangedEvent("AkalCmdChk"); } }

        private Boolean altkCmdChk;
        public Boolean AltkCmdChk { get { return altkCmdChk; } set { altkCmdChk = value; RaisePropertyChangedEvent("AltkCmdChk"); } }

        private Boolean trn6CmdChk;
        public Boolean Trn6CmdChk { get { return trn6CmdChk; } set { trn6CmdChk = value; RaisePropertyChangedEvent("Trn6CmdChk"); } }

        private Boolean trn7CmdChk;
        public Boolean Trn7CmdChk { get { return trn7CmdChk; } set { trn7CmdChk = value; RaisePropertyChangedEvent("Trn7CmdChk"); } }

        private Boolean trn8CmdChk;
        public Boolean Trn8CmdChk { get { return trn8CmdChk; } set { trn8CmdChk = value; RaisePropertyChangedEvent("Trn8CmdChk"); } }

        private Boolean trn9CmdChk;
        public Boolean Trn9CmdChk { get { return trn9CmdChk; } set { trn9CmdChk = value; RaisePropertyChangedEvent("Trn9CmdChk"); } }

        private Boolean trn10CmdChk;
        public Boolean Trn10CmdChk { get { return trn10CmdChk; } set { trn10CmdChk = value; RaisePropertyChangedEvent("Trn10CmdChk"); } }

        private Boolean trn11CmdChk;
        public Boolean Trn11CmdChk { get { return trn11CmdChk; } set { trn11CmdChk = value; RaisePropertyChangedEvent("Trn11CmdChk"); } }


        private Boolean modeSimpleCmdChk;
        public Boolean ModeSimpleCmdChk {
            get { return modeSimpleCmdChk; }
            set { modeSimpleCmdChk = value; RaisePropertyChangedEvent("ModeSimpleCmdChk"); }
        }


        private Boolean addCmdEnable;
        public Boolean AddCmdEnable {
            get { return addCmdEnable; }
            set { addCmdEnable = value; RaisePropertyChangedEvent("AddCmdEnable"); }
        }


        private Boolean deleteCmdEnable;
        public Boolean DeleteCmdEnable { get { return deleteCmdEnable; } set { deleteCmdEnable = value; RaisePropertyChangedEvent("DeleteCmdEnable"); } }


        private string _serverTime;
        public string ServerTime { get { return _serverTime; } set { _serverTime = value; RaisePropertyChangedEvent("ServerTime"); } }


        private bool _connectEnableBtn;
        public bool ConnectEnableBtn { get { return _connectEnableBtn; } set { _connectEnableBtn = value; RaisePropertyChangedEvent("ConnectEnableBtn"); } }
        #endregion
    }
}