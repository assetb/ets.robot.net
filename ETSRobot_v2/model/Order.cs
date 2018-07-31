using altaik.baseapp.vm;
using altaik.baseapp.vm.command;
using ETSRobot_v2.service;
using ETSRobot_v2.vm;
using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ETSRobot_v2.model {
    public class Order : BaseVM {
        #region Variables
        DSSERVERLib.Online tableQuotes;
        DSSERVERLib.GMsgQuoteS msgQuotes;

        private int iCount = 0;
        private Task[] baseTask = new Task[4];
        private ScenaryOrder item;
        private DataBaseManager dataBaseManager = new DataBaseManager();
        private Task logWriter;
        private Boolean proc; // Флаг запуска
        #endregion

        #region Methods
        // Конструктор
        public Order() { }


        // Кнопка обработки заявки
        public ICommand StartCmd { get { return new DelegateCommand(StartOrder); } }


        // Обработка заявки
        public void StartOrder() {
            if (StartCmdName == "Запуск") {
                // Данные заявки
                msgQuotes.FirmID = SelectedFirmBroker.id;
                msgQuotes.Settl_pair = SelectedClient.settlPair;
                msgQuotes.IssueID = SelectedIssue.id;
                msgQuotes.Issue_name = SelectedIssue.name;

                // Параметры таймера и его запуск
                StartCmdName = "Стоп";
                proc = true;

                CheckBids();
            } else {
                proc = false;
                StartCmdName = "Запуск";
            }
        }


        private void CheckBids() {
            if (BottomPrice > CurrentPrice) {
                AppJournal.Write("Order", "Bottom price more than current");

                proc = false;
                StartCmdName = "Запуск";
                MessageBox.Show("Минимальная цена больше стартовой");
            } else {
                if (SelectedMode.type == "Конкурирующий") {
                    if (BottomPrice < CurrentPrice && (CurrentPrice < LastPrice || LastPrice == 0)) {
                        if (LastBroker == null || !LastBroker.ToLower().Contains(SelectedFirmBroker.name.ToLower())) {
                            SendOrder(1);
                        }
                    }
                } else if (SelectedMode.type == "Разовый") {
                    if (LastPrice < BottomPrice) MessageBox.Show("Заявляемая цена меньше минимальной");
                    else SendOrder(2);
                }
            }
        }


        // Отправка заявки
        private string info;
        private void SendOrder(int type) {
            Random rnd = new Random();

            if (type != 2) {
                LastPrice = CurrentPrice - rnd.Next(Convert.ToInt32(Step) + 1);

                if (LastPrice < BottomPrice) LastPrice = BottomPrice;
            } else {
                proc = false;
                StartCmdName = "Запуск";
            }

            msgQuotes.Price = LastPrice;

            DateTime curTime = DateTime.Now;

            bool rez = msgQuotes.Send(out info);

            if (info.Contains("not allowed")) MessageBox.Show("Клиент не допущен на данный торг");
            else if (info.Contains("quotes") && info.Contains("range") && info.Contains("16:00")) MessageBox.Show("Биржевая сессия завершена");
            else if (!info.Contains("been added")) MessageBox.Show($"Ошибка (сообщение с биржи): {info}");

            logWriter = new Task(() => LogWriter(1, curTime.ToString("dd.MM.yyyy hh:mm:ss:fff")));
            logWriter.Start();

            if (PauseTime == null || PauseTime == 0) PauseTime = 3000;

            Thread.Sleep(PauseTime);
        }


        private void LogWriter(int type, string sendTime = "", string msg = "", string lotName = "") {
            if (type == 1) AppJournal.Write("SendOrder", string.Format("Server info: {0}, client: {1}, price: {2}, curTime: {3}, sendTime: {4}", info, msgQuotes.Settl_pair, msgQuotes.Price, DateTime.Now.ToString("dd.MM.yyyy hh:mm:ss:fff"), sendTime), false);
            else AppJournal.WriteLotLog(lotName, "Server", msg);
        }


        // Котировки
        private void FillQuotesData() {
            AppJournal.Write("Order", "Trying open quotes table");

            tableQuotes = new DSSERVERLib.Online();
            tableQuotes.AddRow += TableQuotesAddRow;

            try {
                tableQuotes.Open(DSSERVERLib.ConnectionType.RTSONL_DYNAMIC, "Quote", "issue_name, price, moment, firm_name", "id", null, null, DSSERVERLib.Sort.RTSONL_SORT_EMPTY);
            } catch (Exception ex) {
                AppJournal.Write("Order", "Opening table err: " + ex.ToString());
                MessageBox.Show("Ошибка доступа к таблице котировок!");
            }
        }


        // Определение текущей цены
        void TableQuotesAddRow(int IDConnect, int IDRecord, object Fields) {
            IList collection = (IList)Fields;
            if (collection[0].ToString() == SelectedIssue.name) {
                decimal rez;
                decimal.TryParse(collection[1].ToString(), NumberStyles.Any, new CultureInfo("en-US"), out rez);
                CurrentPrice = rez;
                LastBroker = collection[3].ToString();

                FirmName = collection[3].ToString();

                if (LastBroker.ToLower().Contains(SelectedFirmBroker.name.ToLower())) {
                    if (IsPrimary) IsPrimary = false;
                    else IsPrimary = true;
                } else {
                    if (IsPrimary && proc) CheckBids();
                }

                LogWriter(2, "", string.Format("Firm: {0}, lot: {1}, price: {2}, serverTime: {3}, curTime: {4}", collection[3].ToString(), collection[0].ToString(), collection[1].ToString(), collection[2].ToString(), DateTime.Now.ToString("hh:mm:ss:fff")), collection[0].ToString());
            }

            if (CurrentPrice == 0) CurrentPrice = SelectedIssue.nominal - Step;
        }


        // Параметры заявки
        private void FillMsgQout() {
            msgQuotes = new DSSERVERLib.GMsgQuoteS();

            msgQuotes.Msg_action = 78;
            msgQuotes.Id = 0;
            msgQuotes.type = 65;
            msgQuotes.IssueID = SelectedIssue.id;
            msgQuotes.Issue_name = SelectedIssue.name;
            msgQuotes.Type_wks = 1;
            msgQuotes.Price = SelectedIssue.nominal;
            msgQuotes.Qty = 1;
            msgQuotes.Paycond = 84;
            msgQuotes.Dcc = "";
            msgQuotes.Delivery_days = 10;
            msgQuotes.Settl_pair = SelectedClient.settlPair;
            msgQuotes.Mm = 0;
            msgQuotes.Leave = 1;
            msgQuotes.E_s = 0;
        }
        #endregion

        #region Bindings
        // Номер заявки
        public int Number { get; set; }


        // Фирма-брокер
        private ObservableCollection<Firm> firmBroker = new ObservableCollection<Firm>();
        public ObservableCollection<Firm> FirmBroker {
            get { return firmBroker; }
            set { firmBroker = value; RaisePropertyChangedEvent("FirmBroker"); }
        }


        // Выбранная фирма-брокер
        private Firm selectedfirmBroker;
        public Firm SelectedFirmBroker {
            get { return selectedfirmBroker; }
            set { selectedfirmBroker = value; RaisePropertyChangedEvent("SelectedFirmBroker"); }
        }


        // Клиент
        private ObservableCollection<SettlPair> client = new ObservableCollection<SettlPair>();
        public ObservableCollection<SettlPair> Client {
            get { return client; }
            set { client = value; RaisePropertyChangedEvent("Client"); }
        }


        // Выбранный клиент
        private SettlPair selectedClient;
        public SettlPair SelectedClient {
            get { return selectedClient; }
            set { selectedClient = value; RaisePropertyChangedEvent("SelectedClient"); }
        }


        // Инструмент (лот)
        private ObservableCollection<Issue> issue = new ObservableCollection<Issue>();
        public ObservableCollection<Issue> Issue {
            get { return issue; }
            set { issue = value; RaisePropertyChangedEvent("Issue"); }
        }


        // Выбранный лот
        private Issue selectedIssue;
        public Issue SelectedIssue {
            get { return selectedIssue; }
            set { selectedIssue = value; RaisePropertyChangedEvent("SelectedIssue"); FillQuotesData(); FillMsgQout(); }
        }


        // Минимальная цена
        private Decimal bottomPrice;
        public Decimal BottomPrice {
            get {
                return bottomPrice;
            }
            set {
                bottomPrice = value; RaisePropertyChangedEvent("BottomPrice");
            }
        }


        // Шаг
        private Decimal step;
        public Decimal Step {
            get { return step; }
            set { step = value; RaisePropertyChangedEvent("Step"); }
        }


        // Последняя цена
        private Decimal lastPrice;
        public Decimal LastPrice {
            get { return lastPrice; }
            set {
                lastPrice = value;
                RaisePropertyChangedEvent("LastPrice");
            }
        }


        // Текущая цена
        private Decimal currentPrice;
        public Decimal CurrentPrice {
            get { return currentPrice; }
            set {
                currentPrice = value;
                RaisePropertyChangedEvent("CurrentPrice");
            }
        }


        private string lastBroker;
        public string LastBroker {
            get { return lastBroker; }
            set { lastBroker = value; RaisePropertyChangedEvent("LastBroker"); }
        }


        // Режим
        private ObservableCollection<Mode> modeType = new ObservableCollection<Mode>();
        public ObservableCollection<Mode> ModeType {
            get { return modeType; }
            set { modeType = value; RaisePropertyChangedEvent("ModeType"); }
        }


        // Выбранный режим
        private Mode selectedMode;
        public Mode SelectedMode {
            get { return selectedMode; }
            set { selectedMode = value; RaisePropertyChangedEvent("SelectedMode"); }
        }


        // Текущее место в рейтинге
        private int place;
        public int Place {
            get { return place; }
            set { place = value; RaisePropertyChangedEvent("Place"); }
        }


        // Именованный статус кнопки
        private String startCmdName;
        public String StartCmdName {
            get { return startCmdName; }
            set { startCmdName = value; RaisePropertyChangedEvent("StartCmdName"); }
        }


        private string _firmName;
        public string FirmName {
            get { return _firmName; }
            set { _firmName = value; RaisePropertyChangedEvent("FirmName"); }
        }


        private int _pauseTime;
        public int PauseTime {
            get { return _pauseTime; }
            set { _pauseTime = value; RaisePropertyChangedEvent("PauseTime"); }
        }


        private MainVM _mainVM;
        public MainVM MainVM {
            get { return _mainVM; }
            set { _mainVM = value; RaisePropertyChangedEvent("MainVM"); }
        }


        private bool _isPrimary;
        public bool IsPrimary {
            get { return _isPrimary; }
            set { _isPrimary = value; RaisePropertyChangedEvent("IsPrimary"); }
        }
        #endregion
    }
}