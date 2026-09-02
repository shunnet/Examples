using Microsoft.AspNetCore.Mvc;
using Snet.Model.data;
using Snet.Service.@interface;
using System.Collections.Concurrent;

namespace Snet.Api.Controllers
{
    /// <summary>
    /// IDaq 基础用法演示：打开/关闭/状态/读取/写入/获取基础数据
    /// </summary>
    [ApiController]
    [Route("api/idaq")]
    public class IDaqController : ControllerBase
    {
        private readonly IDaqRegistry _registry;

        public IDaqController(IDaqRegistry registry)
        {
            _registry = registry;
        }

        /// <summary>打开设备（建立连接）</summary>
        [HttpPost("{sn}/on")]
        public async Task<IActionResult> On(string sn)
        {
            var daq = _registry.Get(sn);
            if (daq is null) return NotFound($"SN={sn} 未注册");

            var result = await daq.OnAsync();
            return result.Status ? Ok() : BadRequest(result.Message);
        }

        /// <summary>关闭设备（释放连接）</summary>
        [HttpPost("{sn}/off")]
        public async Task<IActionResult> Off(string sn, bool hardClose = false)
        {
            var daq = _registry.Get(sn);
            if (daq is null) return NotFound($"SN={sn} 未注册");

            var result = await daq.OffAsync(hardClose);
            return result.Status ? Ok() : BadRequest(result.Message);
        }

        /// <summary>获取设备状态</summary>
        [HttpGet("{sn}/status")]
        public async Task<IActionResult> Status(string sn)
        {
            var daq = _registry.Get(sn);
            if (daq is null) return NotFound($"SN={sn} 未注册");

            var result = await daq.GetStatusAsync();
            return Ok(new { result.Status, result.Message });
        }

        /// <summary>获取底层基础对象（Socket/会话等；Sim 无底层对象返回失败属正常）</summary>
        [HttpGet("{sn}/base")]
        public async Task<IActionResult> Base(string sn)
        {
            var daq = _registry.Get(sn);
            if (daq is null) return NotFound($"SN={sn} 未注册");

            var result = await daq.GetBaseObjectAsync();
            var data = result.GetSource<object>();
            return Ok(new { result.Status, result.Message, Data = data?.GetType().Name });
        }

        /// <summary>读取地址（返回 地址名 → 值 字典）</summary>
        [HttpPost("{sn}/read")]
        public async Task<IActionResult> Read(string sn, Address request)
        {
            var daq = _registry.Get(sn);
            if (daq is null) return NotFound($"SN={sn} 未注册");

            var result = await daq.ReadAsync(request);
            if (!result.Status) return BadRequest(result.Message);

            var values = result.GetSource<ConcurrentDictionary<string, AddressValue>>();
            var simplified = values!.ToDictionary(
                kv => kv.Key,
                kv => new { kv.Value.ResultValue, Quality = kv.Value.Quality.ToString(), kv.Value.Message });
            return Ok(simplified);
        }

        /// <summary>写入地址</summary>
        [HttpPost("{sn}/write")]
        public async Task<IActionResult> Write(string sn, ConcurrentDictionary<string, WriteModel> request)
        {
            var daq = _registry.Get(sn);
            if (daq is null) return NotFound($"SN={sn} 未注册");

            var result = await daq.WriteAsync(request);
            return result.Status ? Ok() : BadRequest(result.Message);
        }
    }
}
