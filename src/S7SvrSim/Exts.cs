﻿using MediatR;
using Microsoft.Extensions.DependencyInjection;
using S7Server.Simulator.ViewModels;
using S7Svr.Simulator.MessageHandlers;
using S7Svr.Simulator.ViewModels;
using S7SvrSim.S7Signal;
using S7SvrSim.Services;
using S7SvrSim.Services.Project;
using S7SvrSim.Services.Recent;
using S7SvrSim.Services.S7Blocks;
using S7SvrSim.Services.Settings;
using S7SvrSim.UserControls;
using S7SvrSim.UserControls.Rws;
using S7SvrSim.ViewModels;
using S7SvrSim.ViewModels.Rw;
using S7SvrSim.ViewModels.Signals;
using Splat;
using System;

namespace S7SvrSim
{
    internal static class Exts
    {
        public static IServiceCollection AddS7CoreServices(this IServiceCollection services)
        {
            services.AddMediatR(typeof(MessageNotificationHandler).Assembly);
            services.AddSingleton<PyScriptRunner>();
            services.AddSingleton<IS7ServerService, S7ServerService>();
            services.AddSingleton<IS7DataBlockService, S7DataBlockService>();
            services.AddSingleton<IS7MBlock, S7MBlock>();
            services.AddSingleton<IProjectFactory, ProjectFractory>();
            services.AddSingleton<IS7BlockProvider, S7BlockProvider>();
            services.AddSingleton<IMemCache<SignalType[]>, SignalTypeCache>();
            services.AddSingleton<IMemCache<WatchState>, WatchStateCache>();
            services.AddSingleton<ISignalAddressUesdCollection, SignalAddressUesedCollection>();
            services.AddAddressUsedCalc();
            services.AddScoped<SignalsHelper>();
            services.AddSingleton<ISettingStore, SettingStore>();
            services.AddSingleton<ISettingFactory, SettingFactory>();
            services.AddSetting(new UpdateAddressOptionsConveter(), "UpdateAddressOptions");
            services.AddSetting(new RecentFileConverter(), "RecentFiles");
            services.AddSetting(new SignalExcelOptionConverter(), "SignalExcelOption");
            services.AddSingleton<RecentFilesCollection>();

            return services;
        }


        public static void RegisterViews(this IServiceProvider sp)
        {
            Locator.CurrentMutable.RegisterLazySingletonEx<MsgLoggerVM>(sp);

            Locator.CurrentMutable.RegisterLazySingletonEx<ConfigPyEngineVM>(sp);
            Locator.CurrentMutable.RegisterLazySingletonEx<ConfigSnap7ServerVM>(sp);

            Locator.CurrentMutable.RegisterLazySingletonEx<RunningSnap7ServerVM>(sp);
            Locator.CurrentMutable.RegisterLazySingletonEx<RwTargetVM>(sp);
            Locator.CurrentMutable.RegisterLazySingletonEx<OperationVM>(sp);
            Locator.CurrentMutable.RegisterLazySingletonEx<ProjectVM>(sp);
            Locator.CurrentMutable.RegisterLazySingletonEx<MainVM>(sp);

            Locator.CurrentMutable.RegisterLazySingletonEx<RwBitVM>(sp);
            Locator.CurrentMutable.RegisterLazySingletonEx<RwByteVM>(sp);
            Locator.CurrentMutable.RegisterLazySingletonEx<RwShortVM>(sp);
            Locator.CurrentMutable.RegisterLazySingletonEx<RwUInt32VM>(sp);
            Locator.CurrentMutable.RegisterLazySingletonEx<RwUInt64VM>(sp);
            Locator.CurrentMutable.RegisterLazySingletonEx<RwRealVM>(sp);
            Locator.CurrentMutable.RegisterLazySingletonEx<RwLRealVM>(sp);
            Locator.CurrentMutable.RegisterLazySingletonEx<RwStringVM>(sp);

            Locator.CurrentMutable.RegisterLazySingletonEx<ScriptTaskWindowVM>(sp);
            Locator.CurrentMutable.RegisterLazySingletonEx<UpdateAddressOptionsVM>(sp);
            Locator.CurrentMutable.RegisterLazySingletonEx<SignalsCollection>(sp);
            Locator.CurrentMutable.RegisterLazySingletonEx<DragSignalsVM>(sp);
            Locator.CurrentMutable.RegisterLazySingletonEx<SignalWatchVM>(sp);
            Locator.CurrentMutable.RegisterLazySingletonEx<SignalExcelVM>(sp);
            Locator.CurrentMutable.RegisterLazySingletonEx<SignalPageVM>(sp);

            Locator.CurrentMutable.Register<IViewFor<RwBitVM>, BitOpsView>();
            Locator.CurrentMutable.Register<IViewFor<RwByteVM>, ByteOpsView>();
            Locator.CurrentMutable.Register<IViewFor<RwShortVM>, ShortOpsView>();
            Locator.CurrentMutable.Register<IViewFor<RwUInt32VM>, UInt32OpsView>();
            Locator.CurrentMutable.Register<IViewFor<RwUInt64VM>, UInt64OpsView>();
            Locator.CurrentMutable.Register<IViewFor<RwRealVM>, RealOpsView>();
            Locator.CurrentMutable.Register<IViewFor<RwLRealVM>, LRealOpsView>();
            Locator.CurrentMutable.Register<IViewFor<RwStringVM>, StringOpsView>();
            //Locator.CurrentMutable.Register<IViewFor<ScriptTaskWindowVM>, ScriptTaskWindow>();



        }
    }
}
