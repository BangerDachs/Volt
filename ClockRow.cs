using System.ComponentModel;

namespace Volt
{
    internal sealed class ClockRow : INotifyPropertyChanged
    {
        private string _value;
        private string _avg;
        private string _min;
        private string _max;

        public string Name { get; }
        public string Value
        {
            get => _value;
            set
            {
                if (_value == value)
                    return;

                _value = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            }
        }
        public string Avg
        {
            get => _avg;
            set
            {
                if (_avg == value)
                    return;
                _avg = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Avg)));
            }
        }
        public string Min
        {
            get => _min;
            set
            {
                if (_min == value)
                    return;
                _min = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Min)));
            }
        }
        public string Max
        {
            get => _max;
            set
            {
                if (_max == value)
                    return;
                _max = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Max)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public ClockRow(string name, string value, string avg, string min, string max)
        {
            Name = name;
            _value = value;
            _avg = avg;
            _min = min;
            _max = max;
        }
    }
}
