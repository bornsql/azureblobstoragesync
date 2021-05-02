using System.Runtime.InteropServices;
using RemoteStorageHelper.Enums;

namespace RemoteStorageHelper.Entities
{
	[StructLayout(LayoutKind.Sequential)]
	public class NetResource
	{
		public ResourceScope Scope;
		public ResourceType ResourceType;
		public ResourceDisplayType DisplayType;
		public int Usage;
		public string LocalName;
		public string RemoteName;
		public string Comment;
		public string Provider;
	}
}
