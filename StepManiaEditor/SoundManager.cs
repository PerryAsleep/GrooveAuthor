using System;
using System.Threading.Tasks;
using FMOD;
using Fumen;

namespace StepManiaEditor
{
	public class SoundManager
	{
		private FMOD.System System;

		public SoundManager()
		{
			ErrCheck(Factory.System_Create(out System));
			ErrCheck(System.init(100, INITFLAGS.NORMAL, IntPtr.Zero));
		}

		public async Task<Sound> LoadAsync(string fileName, MODE mode = MODE.DEFAULT)
		{
			return await Task.Run(() =>
			{
				ErrCheck(System.createSound(fileName, mode, out var sound), $"Failed to load {fileName}");
				return sound;
			});
		}

		public void CreateChannelGroup(string name, out ChannelGroup channelGroup)
		{
			ErrCheck(System.createChannelGroup(name, out channelGroup));
		}

		public void PlaySound(Sound sound, ChannelGroup channelGroup, out Channel channel)
		{
			ErrCheck(System.playSound(sound, channelGroup, true, out channel));
		}

		public void Update()
		{
			ErrCheck(System.update());
		}

		public static bool ErrCheck(RESULT result, string failureMessage = null)
		{
			if (result != RESULT.OK)
			{
				if (!string.IsNullOrEmpty(failureMessage))
				{
					Logger.Error($"{failureMessage} {result:G}");
				}
				else
				{
					Logger.Error($"FMOD error: {result:G}");
				}
				return false;
			}
			return true;
		}
	}
}
