using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Xml.Serialization;

namespace GerberToQuspa.ViewModel
{    
    public class DispenseCondition
    {
        public int No { get; set; }
        public string Name { get; set; } = "default";
        public double Size { get; set; } = 0.200;

        private string dirPath = $"{AppDomain.CurrentDomain.BaseDirectory}\\DispenseConditions";

        public String FilePath => $"{dirPath}\\condition_{No}";

        public override string ToString()
        {
            return $"{No}:{Name}";
        }

        public void Save()
        {            
            if (!Directory.Exists(dirPath))
            {
                // ディレクトリを作成する
                Directory.CreateDirectory(dirPath);
            }
            CSettingXMLService<DispenseCondition>.SaveToXml(FilePath, this);
        }        
    }

    public class OperationModel
    {
        public double Size { get; set; }
        public double Px { get; set; } = 0.200;
        public double Py { get; set; } = 0.200;
        public int CountX { get; set; } = 1;
        public int CountY { get; set; } = 1;
        public bool IsAutoPlace { get; set; } = false;
        public double OffsetX { get; set; } = 0.200;
        public double OffsetY { get; set; } = 0.200;
    }

    public class OperationViewModel : BindableBase
    {
        public ObservableCollection<DispenseCondition> Conditions { get; set; } = new ObservableCollection<DispenseCondition>();
        private OperationModel _operationModel { get; set; }

        private string _name;
        public string Name
        {
            get { return _name; }
            set { SetProperty(ref _name, value); }
        }
       
        public double Size
        {
            get { return _operationModel.Size; }
            set
            {
                if (_operationModel.Size != value)
                {
                    _operationModel.Size = value;
                    OnPropertyChanged();
                }
            }
        }

        public double Px
        {
            get { return _operationModel.Px; }
            set
            {
                if (_operationModel.Px != value)
                {
                    _operationModel.Px = value;
                    OnPropertyChanged();
                }
            }
        }

        public double Py
        {
            get { return _operationModel.Py; }
            set
            {
                if (_operationModel.Py != value)
                {
                    _operationModel.Py = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsAutoPlace
        {
            get { return _operationModel.IsAutoPlace; }
            set
            {
                if (_operationModel.IsAutoPlace != value)
                {
                    _operationModel.IsAutoPlace = value;
                    OnPropertyChanged();
                }
            }
        }
                
        public int CountX
        {
            get { return _operationModel.CountX; }
            set
            {
                if (_operationModel.CountX != value)
                {
                    _operationModel.CountX = value;
                    OnPropertyChanged();
                }
            }
        }
        
        public int CountY
        {
            get { return _operationModel.CountY; }
            set
            {
                if (_operationModel.CountY != value)
                {
                    _operationModel.CountY = value;
                    OnPropertyChanged();
                }
            }
        }
       
        public double OffsetX
        {
            get { return _operationModel.OffsetX; }
            set
            {
                if (_operationModel.OffsetX != value)
                {
                    _operationModel.OffsetX = value;
                    OnPropertyChanged();
                }
            }
        }
        
        public double OffsetY
        {
            get { return _operationModel.OffsetY; }
            set
            {
                if (_operationModel.OffsetY != value)
                {
                    _operationModel.OffsetY = value;
                    OnPropertyChanged();
                }
            }
        }

        private DispenseCondition _selectedCondition;
        public DispenseCondition SelectedCondition
        {
            get { return _selectedCondition; }
            set
            {
                SetProperty(ref _selectedCondition, value);
                Name = _selectedCondition.Name;
                Size = _selectedCondition.Size;
            }
        }

        private bool _isEdit = false;
        public bool IsEdit
        {
            get { return _isEdit; }
            set { SetProperty(ref _isEdit, value); }
        }

        public DelegateCommand AddCommand { get; }
        public DelegateCommand EditCommand { get; }
        public DelegateCommand SaveCommand { get; }
        public DelegateCommand CancelCommand { get; }
        
        public DelegateCommand AddPointCommand { get; set; }
        public DelegateCommand CopyPointCommand { get; set; }
        public DelegateCommand MovePointCommand { get; set; }
        public DelegateCommand DeletePointCommand { get; set; }
        public DelegateCommand GroupingPointCommand { get; set; }


        private string dirPath = $"{AppDomain.CurrentDomain.BaseDirectory}\\DispenseConditions";

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="no"></param>
        public OperationViewModel(OperationModel model)
        {
            _operationModel = model;

            LoadConditionList();

            SelectedCondition = Conditions[0];

            AddCommand = new DelegateCommand(Add);
            EditCommand = new DelegateCommand(Edit);
            SaveCommand = new DelegateCommand(Save);
            CancelCommand = new DelegateCommand(Cancel);            
        }

        private void LoadConditionList()
        {
            Conditions.Clear();
            Directory.CreateDirectory(dirPath);

            string[] files = Directory.GetFiles(dirPath);
            foreach (var file in files)
            {
                DispenseCondition condition = CSettingXMLService<DispenseCondition>.LoadFromXml(Path.GetFullPath(file));
                Conditions.Add(condition);
            }

            if(Conditions.Count == 0)
            {
                DispenseCondition condition = new DispenseCondition()
                {
                    No = GetNextConditionNumber(),
                };
                condition.Save();
                Conditions.Add(condition);
            }
        }

        private int GetNextConditionNumber()
        {
            var regex = new Regex(@"condition_(\d+)", RegexOptions.IgnoreCase);

            var set = new HashSet<int>(
                Directory.EnumerateFiles(dirPath)
                         .Select(Path.GetFileNameWithoutExtension)
                         .Select(name =>
                         {
                             var m = regex.Match(name);
                             return m.Success ? int.Parse(m.Groups[1].Value) : -1;
                         })
                         .Where(n => n > 0)
            );

            int i = 1;
            while (set.Contains(i))
                i++;

            return i;
        }

        private void Add()
        {
            DispenseCondition condition = new DispenseCondition()
            {
                No = GetNextConditionNumber(),
            };
            condition.Save();
            Conditions.Add(condition);
            SelectedCondition = condition;
            IsEdit = true;
        }

        private void Edit()
        {
            IsEdit = true;
        }

        private void Save()
        {
            IsEdit = false;
            SelectedCondition.Name = Name;
            SelectedCondition.Size = Size;
            SelectedCondition.Save();
        }

        private void Cancel()
        {
            IsEdit = false;
            SelectedCondition = CSettingXMLService<DispenseCondition>.LoadFromXml(SelectedCondition.FilePath);
        }
    }
}
