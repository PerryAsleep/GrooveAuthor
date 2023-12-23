using System;
using System.Runtime.InteropServices;
using FMOD;

namespace StepManiaEditor;

internal sealed class DspHandle : IDisposable
{
	private bool Disposed;
	public readonly DSP_READCALLBACK Callback;
	private GCHandle Handle;
	private readonly DSP Dsp;

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
