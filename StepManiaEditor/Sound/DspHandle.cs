using System;
using System.Runtime.InteropServices;
using FMOD;

namespace StepManiaEditor;

/// <summary>
/// Handle to an FMOD DSP.
/// </summary>
internal sealed class DspHandle : IDisposable
{
	private bool Disposed;
	public readonly DSP_READCALLBACK Callback;
	private GCHandle Handle;
	private readonly DSP Dsp;

	/// <summary>
	/// Constructor.
	/// </summary>
	/// <param name="system">FMOD System for creating the DSP.</param>
	/// <param name="readCallback">The function to invoke for DSP processing.</param>
	/// <param name="userData">User data to be captured and passed to the callback.</param>
	public DspHandle(FMOD.System system, DSP_READCALLBACK readCallback, object userData)
	{
		Callback = readCallback;
		Handle = GCHandle.Alloc(userData);

		var desc = new DSP_DESCRIPTION()
		{
			numinputbuffers = 1,
			numoutputbuffers = 1,
			read = readCallback,
			userdata = GCHandle.ToIntPtr(Handle),
		};
		SoundManager.ErrCheck(system.createDSP(ref desc, out Dsp));
	}

	~DspHandle()
	{
		Dispose();
	}

	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	private void Dispose(bool disposing)
	{
		if (Disposed)
			return;

		if (disposing)
		{
			Handle.Free();
		}

		Disposed = true;
	}

	public DSP GetDsp()
	{
		return Dsp;
	}
}
