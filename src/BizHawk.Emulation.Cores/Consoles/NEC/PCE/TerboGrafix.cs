using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using BizHawk.BizInvoke;
using BizHawk.Common;
using BizHawk.Emulation.Common;
using BizHawk.Emulation.Cores.PCEngine;
using BizHawk.Emulation.Cores.Waterbox;
using BizHawk.Emulation.DiscSystem;

namespace BizHawk.Emulation.Cores.Consoles.NEC.PCE
{
	[Core(CoreNames.TurboNyma, "Mednafen Team", true, true, "1.24.3", "https://mednafen.github.io/releases/", false)]
	public class TerboGrafix : NymaCore, IRegionable, IPceGpuView
	{
		private readonly LibTerboGrafix _terboGrafix;

		[CoreConstructor(new[] { "PCE", "SGX" })]
		public TerboGrafix(GameInfo game, byte[] rom, CoreComm comm, string extension,
			NymaSettings settings, NymaSyncSettings syncSettings, bool deterministic)
			: base(comm, "PCE", "PC Engine Controller", settings, syncSettings)
		{
			if (game["BRAM"])
				SettingsOverrides["pce.disable_bram_hucard"] = "0";
			_terboGrafix = DoInit<LibTerboGrafix>(game, rom, null, "pce.wbx", extension, deterministic);
		}
		public TerboGrafix(GameInfo game, Disc[] discs, CoreComm comm,
			NymaSettings settings, NymaSyncSettings syncSettings, bool deterministic)
			: base(comm, "PCE", "PC Engine Controller", settings, syncSettings)
		{
			var firmwares = new Dictionary<string, byte[]>();
			var types = discs.Select(d => new DiscIdentifier(d).DetectDiscType())
				.ToList();
			if (types.Contains(DiscType.TurboCD))
				firmwares.Add("FIRMWARE:syscard3.pce", comm.CoreFileProvider.GetFirmware("PCECD", "Bios", true));
			if (types.Contains(DiscType.TurboGECD))
				firmwares.Add("FIRMWARE:gecard.pce", comm.CoreFileProvider.GetFirmware("PCECD", "GE-Bios", true));
			_terboGrafix = DoInit<LibTerboGrafix>(game, null, discs, "pce.wbx", null, deterministic, firmwares);
		}

		public override string SystemId => IsSgx ? "SGX" : "PCE";

		protected override IDictionary<string, string> SettingsOverrides { get; } = new Dictionary<string, string>
		{
			// handled by hawk
			{ "pce.cdbios", null },
			{ "pce.gecdbios", null },
			// so fringe i don't want people bothering me about it
			{ "pce.resamp_rate_error", null },
			{ "pce.vramsize", null },
			// match hawk behavior on BRAM, instead of giving every game BRAM
			{ "pce.disable_bram_hucard", "1" },
			// nyma settings that don't apply here
			// TODO: not quite happy with how this works out
			{ "nyma.rtcinitialtime", null },
			{ "nyma.rtcrealtime", null },
		};

		protected override IDictionary<string, string> ButtonNameOverrides { get; } = new Dictionary<string, string>
		{
			{ "RIGHT →", "Right up my arse" },
		};

		// pce always has two layers, sgx always has 4, and mednafen knows this
		public bool IsSgx => SettingsInfo.LayerNames.Count == 4;

		public unsafe void GetGpuData(int vdc, Action<PceGpuData> callback)
		{
			using(_exe.EnterExit())
			{
				var palScratch = new int[512];
				var v = new PceGpuData();
				_terboGrafix.GetVramInfo(v, vdc);
				fixed(int* p = palScratch)
				{
					for (var i = 0; i < 512; i++)
						p[i] = v.PaletteCache[i] | unchecked((int)0xff000000);
					v.PaletteCache = p;
					callback(v);
				}
				
			}
		}
	}

	public abstract class LibTerboGrafix : LibNymaCore
	{
		[BizImport(CallingConvention.Cdecl, Compatibility = true)]
		public abstract void GetVramInfo([Out]PceGpuData v, int vdcIndex);
	}
}
