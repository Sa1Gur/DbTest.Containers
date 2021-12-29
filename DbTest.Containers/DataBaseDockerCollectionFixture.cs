using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;
using Xunit;

namespace DbTest.Containers
{
	public class DataBaseDockerCollectionFixture
	{
		private readonly DockerDatabaseUtilities _docker = new();
		private string? _dockerContainerId;
		public  string? DockerPort { get; private set; }

		public string Host;
		public string PortInContainer;
		public string DbPassword;
		public string DbUser;
		public string DbName;
		public string DbImage;
		public List<string> Env;

		public DbConnection Connection;

		public async Task InitializeAsync()
		{
			_docker.Host = Host;
			_docker.PortInContainer = PortInContainer;
			_docker.DbPassword = DbPassword;
			_docker.DbUser = DbUser;
			_docker.DbName = DbName;
			_docker.DbImage = DbImage;
			_docker.Env = Env;

			(_dockerContainerId, DockerPort) =
				await _docker.EnsureDockerStartedAndGetContainerIdAndPortAsync(Connection);
		}

		public Task DisposeAsync() => _docker.EnsureDockerContainersStoppedAndRemovedAsync(_dockerContainerId);
	}
}
