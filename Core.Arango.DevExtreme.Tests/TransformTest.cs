using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection.Metadata;
using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Data.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Core.Arango.DevExtreme.Tests
{
    public class TransformTest
    {
        public TransformTest(ITestOutputHelper output)
        {
            _output = output;
        }

        private readonly ITestOutputHelper _output;

        private DataSourceLoadOptionsBase DxLoad(Func<string, string> valueSource)
        {
            var loadOptions = new DataSourceLoadOptionsBase();
            DataSourceLoadOptionsParser.Parse(loadOptions, valueSource);
            return loadOptions;
        }

        [Fact]
        public void BoolTypeTest()
        {
            var at = new ArangoTransform(new DataSourceLoadOptionsBase
            {
                Take = 20,
                Filter = JArray.Parse(@"[[""name"",""=="",true],[""name"",""<>"", false]]")
            }, new ArangoTransformSettings());

            Assert.True(at.Transform(out _));
            var parameter = at.Parameter
                .Select(x => $"{x.Key}: {x.Value} [{x.Value?.GetType()}]")
                .ToList();
            
            Assert.Equal("(x.Name == @P1 && x.Name != @P2)", at.FilterExpression);
            _output.WriteLine(JsonConvert.SerializeObject(parameter, Formatting.Indented));
        }

        
        [Fact]
        public void LookupTest()
        {
            var at = new ArangoTransform(new DataSourceLoadOptionsBase
            {
                Take = 20,
                Filter = JArray.Parse(@"[[""name"",""contains"",""STP""],[""name"",""contains"",""mittelwort""],[""name"",""contains"",""letzteswort""]]")
            }, new ArangoTransformSettings());

            Assert.True(at.Transform(out _));
            Assert.Equal("(LOWER(x.Name) LIKE @P1 && LOWER(x.Name) LIKE @P2 && LOWER(x.Name) LIKE @P3)", at.FilterExpression);
        }

        [Fact]
        public void DateTimeTest()
        {
            var at = new ArangoTransform(new DataSourceLoadOptionsBase
            {
                Take = 20,
                Filter = JArray.Parse(
                    @"[[],""and"",[[""start"","">="",""2020-03-10T23:00:00.000Z""],""and"",[""start"",""<"",""2020-03-11T23:00:00.000Z""]]]")
            }, new ArangoTransformSettings());

            Assert.True(at.Transform(out _));
            var parameter = at.Parameter
                .Select(x => $"{x.Key}: {x.Value} [{x.Value?.GetType()}]")
                .ToList();

            Assert.Equal("(true && (x.Start >= @P1 && x.Start < @P2))", at.FilterExpression);
            _output.WriteLine(JsonConvert.SerializeObject(parameter, Formatting.Indented));
        }

        [Fact]
        public void GroupLookups()
        {
            var at = new ArangoTransform(new DataSourceLoadOptionsBase
            {
                Sort = new[]
                {
                    new SortingInfo
                    {
                        Selector = "start",
                        Desc = true
                    }
                },
                Group = new[]
                {
                    new GroupingInfo
                    {
                        Selector = "projectKey",
                        Desc = false,
                        IsExpanded = false
                    }
                },
                TotalSummary = new[]
                {
                    new SummaryInfo
                    {
                        Selector = "duration",
                        SummaryType = "sum"
                    },
                    new SummaryInfo
                    {
                        Selector = "revenue",
                        SummaryType = "sum"
                    }
                },
                GroupSummary = new[]
                {
                    new SummaryInfo
                    {
                        Selector = "duration",
                        SummaryType = "sum"
                    },
                    new SummaryInfo
                    {
                        Selector = "duration",
                        SummaryType = "sum"
                    },
                    new SummaryInfo
                    {
                        Selector = "revenue",
                        SummaryType = "sum"
                    }
                }
            }, new ArangoTransformSettings
            {
                GroupLookups = new Dictionary<string, string>
                {
                    ["ProjectKey"] = "DOCUMENT(AProject, ProjectKey).Name",
                    ["UserKey"] = "DOCUMENT(AUser, UserKey).Name"
                }
            });

            Assert.True(at.Transform(out _));

            Assert.Equal(@"COLLECT
ProjectKey = x.ProjectKey
AGGREGATE
TotalCount = LENGTH(1), SUMDuration = SUM(x.Duration), SUMRevenue = SUM(x.Revenue)
LET result = {TotalCount, ProjectKey, ProjectKey_DV: DOCUMENT(AProject, ProjectKey).Name, SUMDuration, SUMRevenue}
SORT result.ProjectKey_DV  ASC, ProjectKey  ASC
RETURN result
", at.AggregateExpression);
        }

        [Fact]
        public void InArrayTest()
        {
            var at = new ArangoTransform(new DataSourceLoadOptionsBase
            {
                Take = 20,
                RequireTotalCount = false,
                Filter = JArray.Parse(
                    @"[[""categoryKeys"",""in"",""d9d48fe3-03dc-e611-80dd-0050568a3ed2""],""or"",[""categoryKeys"",""in"",""ad22d4ec-03dc-e611-80dd-0050568a3ed2""]]")
            }, new ArangoTransformSettings());

            Assert.True(at.Transform(out _));
            Assert.Equal("(@P1 IN x.CategoryKeys || @P2 IN x.CategoryKeys)", at.FilterExpression);
        }


        [Fact]
        public void ContainsArrayTest()
        {
            var at = new ArangoTransform(new DataSourceLoadOptionsBase
            {
                Take = 20,
                RequireTotalCount = false,
                Filter = JArray.Parse(
                    @"[""categoryKeys"",""acontains"",""d9d48fe3-03dc-e611-80dd-0050568a3ed2""]")
            }, new ArangoTransformSettings());

            Assert.True(at.Transform(out _));
            Assert.Equal("CONTAINS(LOWER(x.CategoryKeys), @P1)", at.FilterExpression);
        }

        [Fact]
        public void ExtractFilters()
        {
            var at = new ArangoTransform(new DataSourceLoadOptionsBase
            {
                Take = 20,
                RequireTotalCount = false,
                Filter = JArray.Parse(
                    @"[[""categoryKeys"",""in"",""d9d48fe3-03dc-e611-80dd-0050568a3ed2""],""or"",[""categoryKeys"",""in"",""ad22d4ec-03dc-e611-80dd-0050568a3ed2""]]")
            }, new ArangoTransformSettings
            {
                ExtractFilters = new Dictionary<string, ArangoFilterTransform>
                {
                    ["CategoryKeys"] = new ArangoFilterTransform
                    {
                        IteratorVar = "z",
                        Collection = "Project",
                        Property = "CategoryKeys"
                    }
                }
            });

            Assert.True(at.Transform(out _));
            Assert.Equal("(true || true)", at.FilterExpression);
        }

        [Fact]
        public void NegateExpression()
        {
            var at = new ArangoTransform(new DataSourceLoadOptionsBase
            {
                Take = 20,
                RequireTotalCount = true,
                Sort = new[]
                {
                    new SortingInfo
                    {
                        Selector = "start",
                        Desc = true
                    }
                },
                Filter = JArray.Parse(@"[[], ""and"", [""!"", [""scope"", ""=="", ""plan""]]]")
            }, new ArangoTransformSettings());

            Assert.True(at.Transform(out _));
            Assert.Equal("(true && !(x.Scope == @P1))", at.FilterExpression);
        }

        [Fact]
        public void NegateExpression2()
        {
            var loadOptions = DxLoad(key =>
            {
                if (key == "filter")
                    return WebUtility.UrlDecode(
                        @"%5B%22!%22,%5B%5B%22type%22,%22=%22,null%5D,%22or%22,%5B%22type%22,%22=%22,1%5D%5D%5D");
                return null;
            });

            var at = new ArangoTransform(loadOptions, new ArangoTransformSettings());

            Assert.True(at.Transform(out _));
            Assert.Equal("!((x.Type == @P1 || x.Type == @P2))", at.FilterExpression);
        }

        [Fact]
        public void Query()
        {
            var loadOptions = DxLoad(key =>
            {
                var r = key switch
                {
                    "take" => "100",
                    "requireTotalCount" => "true",
                    "sort" => "%5B%7B%22selector%22:%22start%22,%22desc%22:true%7D%5D",
                    "filter" => "%5B%5B%5D,%22and%22,%5B%22!%22,%5B%5B%22userKey%22,%22=%22,%2255770a6d-4dd7-42cf-9db5-aaff00d106d7%22%5D,%22or%22,%5B%22userKey%22,%22=%22,%2240ecb6b1-26cd-44e0-88df-a9457f4ade9c%22%5D%5D%5D%5D",
                    "totalSummary" => "%5B%7B%22selector%22:%22duration%22,%22summaryType%22:%22sum%22%7D,%7B%22selector%22:%22revenue%22,%22summaryType%22:%22sum%22%7D%5D",
                    _ => null
                };

                return key != null ? WebUtility.UrlDecode(r) : null;
            });

            var at = new ArangoTransform(loadOptions, new ArangoTransformSettings
            {
                ExtractFilters = new Dictionary<string, ArangoFilterTransform>
                {
                    ["UserKey"] = new ArangoFilterTransform
                    {
                        IteratorVar = "z",
                        Collection = "AUser",
                        Property = "_key"
                    }
                }
            });

            Assert.True(at.Transform(out _));
            Assert.Equal("(true && !((false || false)))", at.FilterExpression);
        }

        // JArray.Parse differs from AspNetCore
        [Fact]
        public void NullTypeTest()
        {
            var at = new ArangoTransform(new DataSourceLoadOptionsBase
            {
                Take = 20,
                Filter = JArray.Parse(@"[""parentKey"",""="",null]")
            }, new ArangoTransformSettings());

            Assert.True(at.Transform(out _));
            
            var parameter = at.Parameter
                .Select(x => $"{x.Key}: {x.Value} [{x.Value?.GetType()}]")
                .ToList();

            Assert.Equal("x.ParentKey == @P1", at.FilterExpression);
            _output.WriteLine(JsonConvert.SerializeObject(parameter, Formatting.Indented));
        }


        [Fact]
        public void NumberTypeTest()
        {
            var at = new ArangoTransform(new DataSourceLoadOptionsBase
            {
                Take = 20,
                Filter = JArray.Parse(@"[[""duration"","">"",8],""and"",[""duration"","">"",8.5]]")
            }, new ArangoTransformSettings());

            Assert.True(at.Transform(out _));
            var parameter = at.Parameter
                .Select(x => $"{x.Key}: {x.Value} [{x.Value?.GetType()}]")
                .ToList();

            Assert.Equal("(x.Duration > @P1 && x.Duration > @P2)", at.FilterExpression);
            _output.WriteLine(JsonConvert.SerializeObject(parameter, Formatting.Indented));
        }


        // JArray.Parse differs from AspNetCore
        [Fact]
        public void String2TypeTest()
        {
            var at = new ArangoTransform(new DataSourceLoadOptionsBase
            {
                Take = 20,
                Filter = JArray.Parse(@"[""data.invoiceNumber"",""contains"",""123""]")
            }, new ArangoTransformSettings());

            Assert.True(at.Transform(out _));
            var parameter = at.Parameter
                .Select(x => $"{x.Key}: {x.Value} [{x.Value?.GetType()}]")
                .ToList();

            Assert.Equal("LOWER(x.Data.InvoiceNumber) LIKE @P1", at.FilterExpression);
            _output.WriteLine(JsonConvert.SerializeObject(parameter, Formatting.Indented));
        }

        [Fact]
        public void StringNumberTypeTest()
        {
            var at = new ArangoTransform(new DataSourceLoadOptionsBase
            {
                Take = 20,
                Filter = new List<string>
                {
                    "data.invoiceNumber", "startswith", "1234"
                }
            }, new ArangoTransformSettings());

            Assert.True(at.Transform(out _));
            var parameter = at.Parameter
                .Select(x => $"{x.Key}: {x.Value} [{x.Value?.GetType()}]")
                .ToList();

            Assert.Equal("LOWER(x.Data.InvoiceNumber) LIKE @P1", at.FilterExpression);
            _output.WriteLine(JsonConvert.SerializeObject(parameter, Formatting.Indented));
        }

        [Fact]
        public void GroupDayIntervalTest()
        {
            var at = new ArangoTransform(new DataSourceLoadOptionsBase
            {
                Take = 20,
                Group = new[]
                {
                    new GroupingInfo
                    {
                        Selector = "start",
                        GroupInterval = "year",
                        IsExpanded = true
                    },
                    new GroupingInfo
                    {
                        Selector = "start",
                        GroupInterval = "month",
                        IsExpanded = true
                    },
                    new GroupingInfo
                    {
                        Selector = "start",
                        GroupInterval = "day",
                        IsExpanded = true
                    }
                },
                Filter = JArray.Parse(@"[]"),
                
            }, new ArangoTransformSettings()
            {
                IteratorVar = "a",
                PropertyTransform = (propertyName, settings) =>
                {
                    return $"{settings.IteratorVar}.{propertyName}";
                }
            });

            Assert.True(at.Transform(out _));
        }

        [Fact]
        public void NewInTest()
        {
            var loadOptions = DxLoad(key =>
            {
                if (key == "filter")
                    return WebUtility.UrlDecode(
                        @"[%22key%22,[%22ff9e66a6-2544-4ccd-96b5-b2afb54d7b29%22]]");
                return null;
            });

            var at = new ArangoTransform(loadOptions, new ArangoTransformSettings());

            Assert.True(at.Transform(out _));


            _output.WriteLine(at.FilterExpression);
            _output.WriteLine(JsonConvert.SerializeObject(at.Parameter.First()));
        }

        [Fact]
        public void OrAndTest()
        {
            var loadOptions = DxLoad(key =>
            {
                if (key == "filter")
                    return WebUtility.UrlDecode(
                        @"%5B%5B%22searchstring%22%2C%22contains%22%2C%22asdads%22%5D%2C%22and%22%2C%5B%5B%5B%22key%22%2C%22%3D%22%2C%222f8a66f4-367a-4785-ae16-aa040157fc88%22%5D%2C%22or%22%2C%5B%22key%22%2C%22%3D%22%2C%22d3ed9c41-1732-4c6d-8f2d-a9f300dc4f42%22%5D%2C%22or%22%2C%5B%22key%22%2C%22%3D%22%2C%22b22fd5ac-7ec3-4003-a7f3-6ad1f35ad878%22%5D%2C%22or%22%2C%5B%22key%22%2C%22%3D%22%2C%22401871fa-22fd-48c0-b9f9-e06c9d0e3d7e%22%5D%2C%22or%22%2C%5B%22key%22%2C%22%3D%22%2C%22091cfd10-5eaf-4942-b7e0-84662e365749%22%5D%2C%22or%22%2C%5B%22key%22%2C%22%3D%22%2C%226ce4c205-0f54-4ba6-8000-388ceac27e7a%22%5D%2C%22or%22%2C%5B%22key%22%2C%22%3D%22%2C%2217f10194-680f-4b2e-ab8b-8912bbd68734%22%5D%2C%22or%22%2C%5B%22key%22%2C%22%3D%22%2C%22b70bda6f-aeae-411b-b4d2-f183143177d4%22%5D%2C%22or%22%2C%5B%22key%22%2C%22%3D%22%2C%22a5bd14bf-e618-45a0-9ec8-bea721e2f587%22%5D%2C%22or%22%2C%5B%22key%22%2C%22%3D%22%2C%22e2187c8c-e677-4aa9-9e00-d7117989af47%22%5D%2C%22or%22%2C%5B%22key%22%2C%22%3D%22%2C%2215b01cbb-112c-49b9-bf14-c7b57d6a2a8b%22%5D%2C%22or%22%2C%5B%22key%22%2C%22%3D%22%2C%22fb98e956-bec2-4287-92cb-f72a162fccbe%22%5D%2C%22or%22%2C%5B%22key%22%2C%22%3D%22%2C%22985bd70f-cdec-4817-8f36-964af3c07f8e%22%5D%5D%5D%5D");
                return null;
            });

            var at = new ArangoTransform(loadOptions, new ArangoTransformSettings());
            
            Assert.True(at.Transform(out _));
            
            _output.WriteLine(at.FilterExpression);
            _output.WriteLine(JsonConvert.SerializeObject(at.Parameter.First()));
        }
    }
}