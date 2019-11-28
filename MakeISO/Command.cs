using System;
using System.Windows.Input;

namespace MakeISO
{
    public class Command : ICommand
    {
        public Action<object> ExecuteDelegate { get; set; }
        public Predicate<object> CanExecuteDelegate { get; set; }

        public void Execute(object parameter) => ExecuteDelegate?.Invoke(parameter);

        public bool CanExecute(object parameter) => CanExecuteDelegate?.Invoke(parameter) ?? true;

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }
}
