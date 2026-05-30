using Core.Abstraction;
using Core.Models;
using Prism.Events;
using Recipe.Interfaces;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace Module.Services
{
    public class ZScanGlobalVariableLinkService : IZScanGlobalVariableLinkService
    {
        private readonly IRecipePoolService _recipePoolService;
        private readonly IEventAggregator _eventAggregator;
        private bool _isLinked;
        private string _linkedVariableName = string.Empty;
        private GlobalVariableType _linkedVariableType;

        public bool IsLinked => _isLinked;
        public string LinkedVariableName => _linkedVariableName;

        public ZScanGlobalVariableLinkService(IRecipePoolService recipePoolService, IEventAggregator eventAggregator)
        {
            _recipePoolService = recipePoolService;
            _eventAggregator = eventAggregator;
        }

        public bool LinkVariable(string variableName, GlobalVariableType expectedType)
        {
            if (string.IsNullOrEmpty(variableName))
                return false;

            try
            {
                var variables = _recipePoolService.LoadGlobalVariablesAsync(_recipePoolService.CurrentPoolName).Result;
                var target = variables.FirstOrDefault(v => v.Name == variableName);

                if (target == null)
                    return false;

                _isLinked = true;
                _linkedVariableName = variableName;
                _linkedVariableType = expectedType;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void UnlinkVariable()
        {
            _isLinked = false;
            _linkedVariableName = string.Empty;
        }

        public object GetLinkedValue()
        {
            if (!_isLinked || string.IsNullOrEmpty(_linkedVariableName))
                return null;

            try
            {
                var variables = _recipePoolService.LoadGlobalVariablesAsync(_recipePoolService.CurrentPoolName).Result;
                var target = variables.FirstOrDefault(v => v.Name == _linkedVariableName);

                if (target == null)
                    return null;

                return _linkedVariableType switch
                {
                    GlobalVariableType.Double => double.TryParse(target.Value, out double d) ? d : 0.0,
                    GlobalVariableType.Int => int.TryParse(target.Value, out int i) ? i : 0,
                    GlobalVariableType.Bool => bool.TryParse(target.Value, out bool b) ? b : false,
                    GlobalVariableType.String => target.Value,
                    _ => target.Value
                };
            }
            catch
            {
                return null;
            }
        }

        public void WriteBackValue(object value)
        {
            if (!_isLinked || string.IsNullOrEmpty(_linkedVariableName) || value == null)
                return;

            try
            {
                var variables = _recipePoolService.LoadGlobalVariablesAsync(_recipePoolService.CurrentPoolName).Result;
                var target = variables.FirstOrDefault(v => v.Name == _linkedVariableName);

                if (target != null)
                {
                    target.Value = value.ToString();
                    _recipePoolService.SaveGlobalVariablesAsync(_recipePoolService.CurrentPoolName, variables).Wait();
                }
            }
            catch
            {
            }
        }
    }
}
