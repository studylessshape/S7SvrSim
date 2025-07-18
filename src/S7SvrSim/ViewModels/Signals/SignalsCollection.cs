﻿using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using MediatR;
using ReactiveUI.Fody.Helpers;
using S7Svr.Simulator;
using S7SvrSim.Messages;
using S7SvrSim.S7Signal;
using S7SvrSim.Services;
using S7SvrSim.Services.Command;
using S7SvrSim.Services.Settings;
using S7SvrSim.Shared;
using S7SvrSim.UserControls.Signals;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace S7SvrSim.ViewModels.Signals
{
    public partial class SignalsCollection : ViewModelBase
    {
        private readonly IMemCache<WatchState> watchState;
        private readonly SignalsHelper signalsHelper;
        private readonly IMediator mediator;

        public DataGrid Grid { get; set; }

        [Reactive]
        public string GroupName { get; set; }

        public ObservableCollection<SignalEditGroup> SignalGroups { get; } = new ObservableCollection<SignalEditGroup>();

        public ObservableCollection<SignalEditObj> Signals => SignalGroups.Where(sg => sg.Name == GroupName).FirstOrDefault()?.Signals;

        public bool IsSignalsNotNull => Signals != null;

        [ObservableProperty]
        private SignalEditObj selectedEditObj;

        public bool UpdateAddressByDbIndex { get; set; }

        [ObservableProperty]
        private string newGroupName;

        public ICommand NewSignalCommand { get; }
        public ICommand InsertSignalCommand { get; }
        public ICommand RemoveSelectedSignalsCommand { get; }
        public ICommand RemoveSignalCommand { get; }
        public ICommand ClearSignalsCommand { get; }
        public ICommand OrderByCommand { get; }
        public ICommand CopySignalsCommand { get; }
        public ICommand PasteSignalsCommand { get; }

        public ICommand UpdateAddressFromFirstCommand { get; }
        public ICommand UpdateAddressFromFirstSelectedCommand { get; }
        public ICommand UpdateAddressFromSelectedItemsCommand { get; }
        public ICommand ClearAddressCommand { get; }

        public SignalsCollection(IMemCache<WatchState> watchState, ISetting<UpdateAddressOptions> setting, SignalsHelper signalsHelper, IMediator mediator)
        {
            this.watchState = watchState;
            this.signalsHelper = signalsHelper;
            this.mediator = mediator;
            setting.Value.Subscribe(options =>
            {
                UpdateAddressByDbIndex = options.UpdateAddressByDbIndex;
            });

            var watchGroupName = this.WhenAnyValue(vm => vm.GroupName);
            watchGroupName.Subscribe(_ =>
            {
                OnPropertyChanged(nameof(GroupName));
                OnPropertyChanged(nameof(Signals));
            });

            var watchCanEditSignal = watchGroupName.Select(name => !string.IsNullOrEmpty(name));

            NewSignalCommand = ReactiveCommand.Create<Type>(NewSignal, watchCanEditSignal);
            InsertSignalCommand = ReactiveCommand.Create<Type>(InsertSignal, watchCanEditSignal);
            RemoveSelectedSignalsCommand = ReactiveCommand.Create(RemoveSelectedSignals, watchCanEditSignal);
            RemoveSignalCommand = ReactiveCommand.Create<SignalEditObj>(RemoveSignal, watchCanEditSignal);
            ClearSignalsCommand = ReactiveCommand.Create(ClearSignals, watchCanEditSignal);
            OrderByCommand = ReactiveCommand.Create<SignalSortBy>(OrderBy, watchCanEditSignal);
            CopySignalsCommand = ReactiveCommand.Create(CopySignals, watchCanEditSignal);
            PasteSignalsCommand = ReactiveCommand.Create(PasteSignals, watchCanEditSignal);

            UpdateAddressFromFirstCommand = ReactiveCommand.Create(UpdateAddressFromFirst, watchCanEditSignal);
            UpdateAddressFromFirstSelectedCommand = ReactiveCommand.Create(UpdateAddressFromFirstSelected, watchCanEditSignal);
            UpdateAddressFromSelectedItemsCommand = ReactiveCommand.Create(UpdateAddressFromSelectedItems, watchCanEditSignal);
            ClearAddressCommand = ReactiveCommand.Create(ClearAddress, watchCanEditSignal);
        }

        private void CommandEventHandle(object _object, EventArgs _args)
        {
            ((MainWindow)System.Windows.Application.Current.MainWindow).SwitchTab(2);
        }

        private void RegistCommandEventHandle(IHistoryCommand command)
        {
            command.AfterExecute += CommandEventHandle;
            command.AfterUndo += CommandEventHandle;
        }

        private void SetGridSelectedItems(IEnumerable<SignalEditObj> signals)
        {
            Grid.UnselectAll();
            foreach (var item in signals)
            {
                Grid.SelectedItems.Add(item);
            }
            if (signals.Any())
            {
                Grid.ScrollIntoView(signals.First());
            }
        }

        #region Group Edit
        private async Task<ShowDialogResult> GetNewGroupName(string title, string oldName, bool checkOldName = false)
        {
            var dialogViewModel = new DialogViewModel(title, "名称不能为空或重复")
            {
                Text = oldName,
            };
            dialogViewModel.ValidationEvent += (rawValue) =>
            {
                if (rawValue is string value)
                {
                    if (string.IsNullOrEmpty(value)) return false;

                    if (checkOldName) return !SignalGroups.Any(s => s.Name == value);
                    else return !SignalGroups.Where(s => s.Name != oldName).Any(s => s.Name == value);
                }

                return true;
            };

            var renameResult = await mediator.Send(new ShowDialogRequest(dialogViewModel));
            return renameResult;
        }

        [RelayCommand]
        private void AddGroup()
        {
            if (string.IsNullOrEmpty(NewGroupName) || SignalGroups.Any(sg => sg.Name == NewGroupName)) return;

            var newGroupName = NewGroupName;
            var currentGroupName = GroupName;

            var command = ListChangedCommand.Add(SignalGroups, [new SignalEditGroup(newGroupName, [])]);
            RegistCommandEventHandle(command);
            command.AfterExecute += (_, _) => GroupName = newGroupName;
            command.AfterUndo += (_, _) => GroupName = currentGroupName;
            UndoRedoManager.Run(command);

            NewGroupName = "";
        }

        [RelayCommand]
        private void SwitchGroup(string name)
        {
            GroupName = name;
        }

        [RelayCommand]
        private void DeleteGroup(SignalEditGroup sg)
        {
            if (MessageBox.Show("是否删除？", "请确认", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;

            if (sg == null) return;

            var deleteName = sg.Name;

            var command = ListChangedCommand.Remove(SignalGroups, [sg]);
            RegistCommandEventHandle(command);
            command.AfterExecute += (_, _) =>
            {
                if (deleteName == GroupName || GroupName == null)
                {
                    GroupName = SignalGroups.FirstOrDefault()?.Name;
                }
            };
            command.AfterUndo += (_, _) => GroupName = deleteName;
            UndoRedoManager.Run(command);
        }

        [RelayCommand]
        private async Task RenameGroup(SignalEditGroup sg)
        {
            if (sg == null) return;

            var oldName = sg.Name;

            var renameResult = await GetNewGroupName("Rename Group", oldName);
            if (renameResult.IsCancel || string.IsNullOrEmpty(renameResult.Result)) return;

            var newName = renameResult.Result;
            if (oldName == newName) return;

            var command = new ValueChangedCommand<string>(val => sg.Name = val, oldName, newName);
            RegistCommandEventHandle(command);
            command.AfterExecute += (_, _) =>
            {
                if (GroupName == oldName) GroupName = newName;
            };
            command.AfterUndo += (_, _) =>
            {
                if (GroupName == newName) GroupName = oldName;
            };
            UndoRedoManager.Run(command);
        }

        [RelayCommand]
        private async Task CopyGroup(SignalEditGroup sg)
        {
            if (sg == null) return;

            var renameResult = await GetNewGroupName("Rename Group", sg.Name, true);
            if (renameResult.IsCancel || string.IsNullOrEmpty(renameResult.Result)) return;

            var newSg = new SignalEditGroup(renameResult.Result, sg.Signals);
            var command = ListChangedCommand.Insert(SignalGroups, SignalGroups.IndexOf(sg) + 1, [newSg]);
            RegistCommandEventHandle(command);
            command.AfterUndo += (_, _) =>
            {
                if (GroupName == newSg.Name || string.IsNullOrEmpty(GroupName)) GroupName = SignalGroups.FirstOrDefault()?.Name;
            };
            UndoRedoManager.Run(command);
        }
        #endregion

        #region Signal Edit
        public void OpenValueSet()
        {
            if (SelectedEditObj == null || SelectedEditObj.Value.Address == null || !watchState.Value.IsInWatch || SelectedEditObj.Value is HoldingSignal)
            {
                return;
            }
            var setWindow = new SetSignalValueWindow();
            setWindow.viewModel.SelectedSignal = SelectedEditObj;

            setWindow.ShowDialog();
        }

        private void NewSignal(Type signalType)
        {
            var newSignal = new SignalEditObj(signalType);
            var command = ListChangedCommand.Add(Signals, [newSignal]);
            RegistCommandEventHandle(command);
            command.AfterExecute += (_, _) => SetGridSelectedItems([newSignal]);
            UndoRedoManager.Run(command);
        }

        private void InsertSignals(IEnumerable<SignalEditObj> objs)
        {
            var indexInsert = (Grid.SelectedItems.Count == 0) ? -1 : Signals.IndexOf(Grid.SelectedItems.Cast<SignalEditObj>().OrderBy(Signals.IndexOf).Last()) + 1;
            var command = ListChangedCommand.Insert(Signals, indexInsert, objs);
            RegistCommandEventHandle(command);
            command.AfterExecute += (_, _) => SetGridSelectedItems(objs);
            UndoRedoManager.Run(command);
        }

        private void InsertSignal(Type signalType)
        {
            var newSignal = new SignalEditObj(signalType);
            InsertSignals([newSignal]);
        }

        private void RemoveSignal(SignalEditObj signal)
        {
            var command = ListChangedCommand.Remove(Signals, [signal]);
            RegistCommandEventHandle(command);
            command.AfterUndo += (_, _) => SetGridSelectedItems([signal]);
            UndoRedoManager.Run(command);
        }

        private void RemoveSelectedSignals()
        {
            if (Signals.Count == 0 || Grid.SelectedItems.Count == 0)
            {
                return;
            }
            var removed = Grid.SelectedItems.Cast<SignalEditObj>().ToArray();
            var command = ListChangedCommand.Remove(Signals, removed);
            RegistCommandEventHandle(command);
            command.AfterUndo += (_, _) => SetGridSelectedItems(removed);
            UndoRedoManager.Run(command);
        }

        private void ClearSignals()
        {
            if (Signals.Count == 0)
            {
                return;
            }

            if (MessageBox.Show("确认要删除所有信号吗？", "警告", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            var command = ListChangedCommand.Clear(Signals);
            RegistCommandEventHandle(command);
            UndoRedoManager.Run(command);
        }

        private SignalSortBy? lastSignalSoryBy;

        private void OrderBy(SignalSortBy sortBy)
        {
            IHistoryCommand command;
            switch (sortBy)
            {
                case SignalSortBy.Name:
                    command = lastSignalSoryBy == SignalSortBy.Name ? ListChangedCommand.OrderByDescending(Signals, s => s.Value.Name) : ListChangedCommand.OrderBy(Signals, s => s.Value.Name);
                    break;
                case SignalSortBy.Address:
                    command = lastSignalSoryBy == SignalSortBy.Address ? ListChangedCommand.OrderByDescending(Signals, s => s.Value.Address) : ListChangedCommand.OrderBy(Signals, s => s.Value.Address);
                    break;
                case SignalSortBy.Type:
                    command = lastSignalSoryBy == SignalSortBy.Type ? ListChangedCommand.OrderByDescending(Signals, s => s.Other.Name) : ListChangedCommand.OrderBy(Signals, s => s.Other.Name);
                    break;
                default:
                    return;
            }
            RegistCommandEventHandle(command);
            var lastSignalSoryByCp = lastSignalSoryBy;
            command.AfterExecute += (_, _) =>
            {
                switch (lastSignalSoryBy)
                {
                    case SignalSortBy.Name:
                    case SignalSortBy.Address:
                    case SignalSortBy.Type:
                        lastSignalSoryBy = null;
                        break;
                    case null:
                        lastSignalSoryBy = sortBy;
                        break;
                }
            };
            command.AfterUndo += (_, _) =>
            {
                lastSignalSoryBy = lastSignalSoryByCp;
            };
            UndoRedoManager.Run(command);
        }

        private void CopySignals()
        {
            if (Grid.SelectedItems.Count == 0) return;

            var signals = Grid.SelectedItems.Cast<SignalEditObj>().OrderBy(Signals.IndexOf);
            Clipboard.SetText(signals.ToXml(), TextDataFormat.Xaml);
        }

        private void PasteSignals()
        {
            var clipData = Clipboard.GetText(TextDataFormat.Xaml);
            IEnumerable<SignalEditObj> signals;
            try
            {
                signals = clipData.FromXml(signalsHelper);
            }
            catch (Exception)
            {
                return;
            }
            InsertSignals(signals);
        }
        #endregion

        #region Quick Cal Address
        private IEnumerable<IEnumerable<SignalBase>> AssembleSignalByAddress(IEnumerable<SignalEditObj> target)
        {
            Dictionary<int, List<SignalBase>> signalGroupByDbIndex = new Dictionary<int, List<SignalBase>>();
            int preDbIndex = -1;
            foreach (var signal in target)
            {
                if (signal.Value.Address == null)
                {
                    if (preDbIndex != -1)
                    {
                        signalGroupByDbIndex[preDbIndex].Add(signal.Value);
                    }
                    continue;
                }

                if (signalGroupByDbIndex.TryGetValue(signal.Value.Address.DbIndex, out var dbSignals))
                {
                    dbSignals.Add(signal.Value);
                }
                else
                {
                    signalGroupByDbIndex.Add(signal.Value.Address.DbIndex, new List<SignalBase> { signal.Value });
                }
                preDbIndex = signal.Value.Address.DbIndex;
            }
            return signalGroupByDbIndex.Values;
        }

        private void UpdateAddressFromFirst()
        {
            UndoRedoManager.StartTransaction();

            if (UpdateAddressByDbIndex)
            {
                var dbSignals = AssembleSignalByAddress(Signals);
                dbSignals.Each(signals => signalsHelper.UpdateAddress(signals));
            }
            else
            {
                signalsHelper.UpdateAddress(Signals.Select(s => s.Value));
            }

            var command = UndoRedoManager.EndTransaction();
            RegistCommandEventHandle(command);
        }

        private void UpdateAddressFromFirstSelected()
        {
            if (Grid.SelectedItems.Count == 0)
            {
                return;
            }

            var signals = Signals.Skip(Signals.IndexOf(Grid.SelectedItems.Cast<SignalEditObj>().OrderBy(Signals.IndexOf).First()));

            UndoRedoManager.StartTransaction();

            if (UpdateAddressByDbIndex)
            {
                var dbSignals = AssembleSignalByAddress(signals);
                dbSignals.Each(signals => signalsHelper.UpdateAddress(signals));
            }
            else
            {
                signalsHelper.UpdateAddress(signals.Select(s => s.Value));
            }

            var command = UndoRedoManager.EndTransaction();
            RegistCommandEventHandle(command);
        }

        private void UpdateAddressFromSelectedItems()
        {
            var signals = Grid.SelectedItems.Cast<SignalEditObj>().OrderBy(Signals.IndexOf);
            if (!signals.Any())
            {
                return;
            }

            UndoRedoManager.StartTransaction();

            if (UpdateAddressByDbIndex)
            {
                var dbSignals = AssembleSignalByAddress(signals);
                dbSignals.Each(signals => signalsHelper.UpdateAddress(signals));
            }
            else
            {
                signalsHelper.UpdateAddress(signals.Select(s => s.Value));
            }

            var command = UndoRedoManager.EndTransaction();
            RegistCommandEventHandle(command);
        }

        private void ClearAddress()
        {
            UndoRedoManager.StartTransaction();

            Signals.Each(s =>
            {
                if (s.Value.Address != null)
                {
                    var command = new ValueChangedCommand<SignalAddress>(address =>
                    {
                        s.Value.Address = address;
                    }, s.Value.Address, null);
                    UndoRedoManager.Run(command);
                }
            });

            var command = UndoRedoManager.EndTransaction();
            RegistCommandEventHandle(command);
        }
        #endregion
    }
}
