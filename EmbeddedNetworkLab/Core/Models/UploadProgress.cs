using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EmbeddedNetworkLab.Core.Models
{
	public record UploadProgress(long BytesReceived, long TotalBytes, double Percent);
}
