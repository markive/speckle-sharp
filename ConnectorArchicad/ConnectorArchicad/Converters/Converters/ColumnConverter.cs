using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Archicad.Communication;
using Archicad.Model;
using Objects.Geometry;
using Speckle.Core.Kits;
using Speckle.Core.Models;
using Speckle.Core.Models.GraphTraversal;

namespace Archicad.Converters;

public sealed class Column : IConverter
{
  public Type Type => typeof(Objects.BuiltElements.Column);

  public async Task<List<ApplicationObject>> ConvertToArchicad(
    IEnumerable<TraversalContext> elements,
    CancellationToken token
  )
  {
    var columns = new List<Objects.BuiltElements.Archicad.ArchicadColumn>();

    var context = Archicad.Helpers.Timer.Context.Peek;
    using (
      context?.cumulativeTimer?.Begin(ConnectorArchicad.Properties.OperationNameTemplates.ConvertToNative, Type.Name)
    )
    {
      foreach (var tc in elements)
      {
        token.ThrowIfCancellationRequested();

        switch (tc.current)
        {
          case Objects.BuiltElements.Archicad.ArchicadColumn archicadColumn:
            columns.Add(archicadColumn);
            break;
          case Objects.BuiltElements.Column column:
            var baseLine = (Line)column.baseLine;
            Objects.BuiltElements.Archicad.ArchicadColumn newColumn =
              new()
              {
                id = column.id,
                applicationId = column.applicationId,
                archicadLevel = Archicad.Converters.Utils.ConvertLevel(column.level),
                origoPos = Utils.ScaleToNative(baseLine.start),
                height = Math.Abs(
                  Utils.ScaleToNative(baseLine.end.z, baseLine.end.units)
                    - Utils.ScaleToNative(baseLine.start.z, baseLine.start.units)
                )
              };

            columns.Add(newColumn);
            break;
        }
      }
    }

    IEnumerable<ApplicationObject> result;
    result = await AsyncCommandProcessor.Execute(new Communication.Commands.CreateColumn(columns), token);
    return result is null ? new List<ApplicationObject>() : result.ToList();
  }

  public async Task<List<Base>> ConvertToSpeckle(IEnumerable<Model.ElementModelData> elements, CancellationToken token)
  {
    var elementModels = elements as ElementModelData[] ?? elements.ToArray();
    Speckle.Newtonsoft.Json.Linq.JArray jArray = await AsyncCommandProcessor.Execute(
      new Communication.Commands.GetColumnData(elementModels.Select(e => e.applicationId)),
      token
    );

    var columns = new List<Base>();
    if (jArray is null)
    {
      return columns;
    }

    var context = Archicad.Helpers.Timer.Context.Peek;
    using (
      context?.cumulativeTimer?.Begin(ConnectorArchicad.Properties.OperationNameTemplates.ConvertToSpeckle, Type.Name)
    )
    {
      foreach (Speckle.Newtonsoft.Json.Linq.JToken jToken in jArray)
      {
        // convert between DTOs
        Objects.BuiltElements.Archicad.ArchicadColumn column =
          Archicad.Converters.Utils.ConvertDTOs<Objects.BuiltElements.Archicad.ArchicadColumn>(jToken);

        column.units = Units.Meters;
        column.displayValue = Operations.ModelConverter.MeshesToSpeckle(
          elementModels.First(e => e.applicationId == column.applicationId).model
        );
        column.baseLine = new Line(column.origoPos, column.origoPos + new Point(0, 0, column.height));
        columns.Add(column);
      }
    }

    return columns;
  }
}
