﻿﻿﻿using Core.Abstraction;
using Core.Models;
using Core.Utilities;
using System.IO;
using System.Xml.Linq;

namespace Core.Configuration
{
    public class XmlConfigurationProvider : IConfigurationProvider
    {
        private readonly string _configPath;
        private readonly ILoggerService _logger;

        public bool ConfigurationExists => File.Exists(_configPath);

        public XmlConfigurationProvider(string configPath, ILoggerService logger)
        {
            _configPath = configPath;
            _logger = logger;

            // 确保配置目录存在
            Directory.CreateDirectory(Path.GetDirectoryName(_configPath));
        }

        public T GetConfiguration<T>() where T : class, new()
        {
            try
            {
                if (!ConfigurationExists)
                {
                    _logger.Warn($"配置文件不存在: {_configPath}");
                    return new T();
                }

                var doc = XDocument.Load(_configPath);
                return DeserializeFromXml<T>(doc.Root);
            }
            catch (Exception ex)
            {
                _logger.Error($"加载配置失败: {ex.Message}");
                return new T();
            }
        }

        public void SaveConfiguration<T>(T config) where T : class
        {
            try
            {
                var doc = SerializeToXml(config);
                doc.Save(_configPath);
                _logger.Info($"配置已保存: {_configPath}");
            }
            catch (Exception ex)
            {
                _logger.Error($"保存配置失败: {ex.Message}");
                throw;
            }
        }

        public void CreateDefaultConfiguration()
        {
            try
            {
                var defaultConfig = new AppSettings
                {
                    RecipeName = "Default",
                    LastRecipeName = "Default",
                    LastSelectedRecipePath = string.Empty,
                    Server = new ServerConfiguration
                    {
                        ServerIP = "0.0.0.0",
                        Port = 8080,
                        MaxClients = 100,
                        EncodingMethod = "UTF-8"
                    },
                    Clients = new List<ClientConfiguration>
                    {
                        new ClientConfiguration
                        {
                            ClientName = "PLC1",
                            IP = "192.168.1.10",
                            Port = 9102,
                            Description = "主PLC控制器"
                        },
                        new ClientConfiguration
                        {
                            ClientName = "Vision1",
                            IP = "192.168.1.20",
                            Port = 9102,
                            Description = "视觉系统"
                        }
                    }
                };

                SaveConfiguration(defaultConfig);
                _logger.Info("默认配置文件已创建");
            }
            catch (Exception ex)
            {
                _logger.Error($"创建默认配置失败: {ex.Message}");
                throw;
            }
        }

        private T DeserializeFromXml<T>(XElement element) where T : class, new()
        {
            var config = new T();

            if (typeof(T) == typeof(AppSettings))
            {
                var appConfig = config as AppSettings;
                if (appConfig != null)
                {
                    appConfig.RecipeName = element.Element("RecipeName")?.Value ?? "Default";
                    appConfig.LastRecipeName = element.Element("LastRecipeName")?.Value ?? "Default";
                    appConfig.LastSelectedRecipePath = element.Element("LastSelectedRecipePath")?.Value ?? string.Empty;

                    // 反序列化服务器配置
                    var serverElement = element.Element("Server");
                    if (serverElement != null)
                    {
                        appConfig.Server.ServerIP = serverElement.Element("IP")?.Value ?? "0.0.0.0";
                        appConfig.Server.Port = int.Parse(serverElement.Element("Port")?.Value ?? "8080");
                        appConfig.Server.MaxClients = int.Parse(serverElement.Element("MaxClients")?.Value ?? "100");
                        appConfig.Server.EncodingMethod = serverElement.Element("EncodingMethod")?.Value ?? "UTF-8";
                    }

                    // 反序列化客户端配置 - 支持动态客户端
                    appConfig.Clients.Clear();
                    var clientsElement = element.Element("Clients");
                    if (clientsElement != null)
                    {
                        foreach (var clientElement in clientsElement.Elements("Client"))
                        {
                            var client = new ClientConfiguration
                            {
                                ClientName = clientElement.Element("ClientName")?.Value ?? string.Empty,
                                Mode = clientElement.Element("Mode")?.Value ?? "Client",
                                IP = clientElement.Element("IP")?.Value ?? "127.0.0.1",
                                Port = int.Parse(clientElement.Element("Port")?.Value ?? "8080"),
                                Description = clientElement.Element("Description")?.Value ?? string.Empty,
                                IsEnabled = bool.Parse(clientElement.Element("IsEnabled")?.Value ?? "true")
                            };
                            appConfig.Clients.Add(client);
                        }
                    }
                    else
                    {
                        // 向后兼容：读取旧的固定客户端配置
                        MigrateFromLegacyFormat(element, appConfig);
                    }
                }
            }

            return config;
        }

        private void MigrateFromLegacyFormat(XElement element, AppSettings appConfig)
        {
            // 迁移 Client1
            var client1Element = element.Element("Client1");
            if (client1Element != null)
            {
                appConfig.Clients.Add(new ClientConfiguration
                {
                    ClientName = client1Element.Element("ClientName")?.Value ?? "Client1",
                    IP = client1Element.Element("IP")?.Value ?? "127.0.0.1",
                    Port = int.Parse(client1Element.Element("Port")?.Value ?? "8080"),
                    Description = "迁移自旧配置 Client1"
                });
            }

            // 迁移 Client2
            var client2Element = element.Element("Client2");
            if (client2Element != null)
            {
                appConfig.Clients.Add(new ClientConfiguration
                {
                    ClientName = client2Element.Element("ClientName")?.Value ?? "Client2",
                    IP = client2Element.Element("IP")?.Value ?? "127.0.0.1",
                    Port = int.Parse(client2Element.Element("Port")?.Value ?? "8080"),
                    Description = "迁移自旧配置 Client2"
                });
            }

            _logger.Info("已从旧配置格式迁移到新格式");
        }

        private XDocument SerializeToXml<T>(T config) where T : class
        {
            if (config is AppSettings appConfig)
            {
                var doc = new XDocument(
                    new XElement("Configuration",
                        new XAttribute("Version", "2.0"), // 版本升级
                        new XElement("RecipeName", appConfig.RecipeName),
                        new XElement("LastRecipeName", appConfig.LastRecipeName),
                        new XElement("LastSelectedRecipePath", appConfig.LastSelectedRecipePath),
                        new XElement("Server",
                            new XElement("IP", appConfig.Server.ServerIP),
                            new XElement("Port", appConfig.Server.Port),
                            new XElement("MaxClients", appConfig.Server.MaxClients),
                            new XElement("EncodingMethod", appConfig.Server.EncodingMethod)
                        ),
                        new XElement("Clients",
                            appConfig.Clients.Select(client =>
                                new XElement("Client",
                                    new XElement("ClientName", client.ClientName),
                                    new XElement("Mode", client.Mode),
                                    new XElement("IP", client.IP),
                                    new XElement("Port", client.Port),
                                    new XElement("Description", client.Description),
                                    new XElement("IsEnabled", client.IsEnabled)
                                )
                            )
                        )
                    )
                );

                return doc;
            }

            throw new NotSupportedException($"不支持序列化类型: {typeof(T).Name}");
        }
    }
}
