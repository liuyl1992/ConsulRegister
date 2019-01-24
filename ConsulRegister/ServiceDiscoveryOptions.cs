using System;
using System.Collections.Generic;
using System.Text;

namespace ConsulRegister
{
    /// <summary>
    /// 服务治理第三方组件Consul相关配置参数
    /// </summary>
    public class ServiceDiscoveryOptions
    {
        public string ServiceName { get; set; }

        /// <summary>
        /// 当前服务所在地址host:port
        /// 格式："127.0.0.1:666"
        /// </summary>
        public string SelfAddress { get; set; }
        
        public ConsulOptions Consul { get; set; }
    }

    public class ConsulOptions
    {
        public string HttpEndPoint { get; set; }
    }
}
