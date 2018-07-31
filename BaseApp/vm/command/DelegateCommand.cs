using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;

namespace altaik.baseapp.vm.command
{
    public class DelegateCommand : ICommand
    {
        private readonly Action _action;
        private readonly Func<bool> _canExecute;

        public DelegateCommand(Action action)
        {
            _action = action;
        }


        public DelegateCommand(Action action, Func<bool> canExecute)
        {
            _action = action;
            _canExecute = canExecute;
        }


        public void Execute(object parameter)
        {
            _action();
        }


        public bool CanExecute(object parameter)
        {
            return true;
        }


#pragma warning disable 67
        public event EventHandler CanExecuteChanged;
#pragma warning restore 67
    }
}
