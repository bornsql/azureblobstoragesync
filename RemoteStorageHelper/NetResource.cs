using System.Runtime.InteropServices;

namespace RemoteStorageHelper
{
	[StructLayout(LayoutKind.Sequential)]
	public class NetResource
	{
		public ResourceScope Scope;
		public ResourceType ResourceType;
		public ResourceDisplaytype DisplayType;
		public int Usage;
		public string LocalName;
		public string RemoteName;
		public string Comment;
		public string Provider;
	}
}