using System.Text.Json;
using KeyboardStudio.Core;
using Xunit;

namespace KeyboardStudio.Core.Tests;

public sealed class KeyboardTemplateSchemaTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void Deserialize_WhenTemplateUsesCurrentSchema_PreservesPhysicalGeometry()
    {
        const string json = """
            {
              "schemaVersion": 1,
              "id": "test-layout",
              "name": "Test layout",
              "unitWidth": 54,
              "unitGap": 4,
              "keys": [
                {
                  "id": "KeyA",
                  "scanCode": 30,
                  "extended": true,
                  "x": 1.75,
                  "y": 3,
                  "width": 1,
                  "height": 1
                }
              ]
            }
            """;

        var template = Assert.IsType<KeyboardTemplateDto>(
            JsonSerializer.Deserialize<KeyboardTemplateDto>(json, SerializerOptions));

        Assert.Equal(KeyboardTemplateSchema.CurrentVersion, template.SchemaVersion);
        Assert.Equal("test-layout", template.Id);
        Assert.Equal("Test layout", template.Name);
        Assert.Equal(54, template.UnitWidth);
        Assert.Equal(4, template.UnitGap);

        var key = Assert.Single(template.Keys);
        Assert.Equal("KeyA", key.Id);
        Assert.Equal(30, key.ScanCode);
        Assert.True(key.Extended);
        Assert.Equal(1.75, key.X);
        Assert.Equal(3, key.Y);
        Assert.Equal(1, key.Width);
        Assert.Equal(1, key.Height);
    }

    [Fact]
    public void Deserialize_WhenExtendedFlagIsOmitted_DefaultsToFalse()
    {
        const string json = """
            {
              "schemaVersion": 1,
              "id": "test-layout",
              "name": "Test layout",
              "unitWidth": 54,
              "unitGap": 4,
              "keys": [
                {
                  "id": "KeyA",
                  "scanCode": 30,
                  "x": 0,
                  "y": 0,
                  "width": 1,
                  "height": 1
                }
              ]
            }
            """;

        var template = Assert.IsType<KeyboardTemplateDto>(
            JsonSerializer.Deserialize<KeyboardTemplateDto>(json, SerializerOptions));

        var key = Assert.Single(template.Keys);
        Assert.False(key.Extended);
    }

    [Fact]
    public void Serialize_WhenTemplateDtoIsUsed_DoesNotContainProjectMappings()
    {
        var template = new KeyboardTemplateDto
        {
            SchemaVersion = KeyboardTemplateSchema.CurrentVersion,
            Id = "test-layout",
            Name = "Test layout",
            UnitWidth = 54,
            UnitGap = 4,
            Keys =
            [
                new PhysicalKeyTemplateDto
                {
                    Id = "KeyA",
                    ScanCode = 30,
                    X = 0,
                    Y = 0,
                    Width = 1,
                    Height = 1
                }
            ]
        };

        var json = JsonSerializer.Serialize(template, SerializerOptions);

        Assert.DoesNotContain("mapping", json.ToLowerInvariant());
        Assert.DoesNotContain("output", json.ToLowerInvariant());
    }
}
