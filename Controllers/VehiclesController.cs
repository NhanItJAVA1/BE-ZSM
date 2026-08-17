using BE_ZSM.Contexts;
using BE_ZSM.DTOs.Vehicles;
using BE_ZSM.Entities;
using BE_ZSM.Helpers;
using BE_ZSM.Services;
using BE_ZSM.Services.Vehicle;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BE_ZSM.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class VehiclesController : ControllerBase
    {
        private readonly IVehicleService _vehicleService;

        public VehiclesController(IVehicleService vehicleService)
        {
            _vehicleService = vehicleService;
        }

        [HttpGet]
        public async Task<IActionResult> GetVehicles()
        {
            var vehicles = await _vehicleService.GetVehiclesAsync();

            return Ok(vehicles);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetVehicle(int id)
        {

            var vehicle = await _vehicleService.GetVehicleAsync(id);

            return Ok(vehicle);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateVehicle(
            CreateVehicleDto dto)
        {
            var vehicle =
            await _vehicleService.CreateVehicleAsync(dto);

            return CreatedAtAction(
                nameof(GetVehicle),
                new { id = vehicle.Id },
                vehicle);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateVehicle(
            int id,
            UpdateVehicleDto dto)
        {
            var vehicle =
            await _vehicleService.UpdateVehicleAsync(id, dto);

            return Ok(vehicle);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteVehicle(int id)
        {
            await _vehicleService.DeleteVehicleAsync(id);

            return NoContent();
        }
    }
}