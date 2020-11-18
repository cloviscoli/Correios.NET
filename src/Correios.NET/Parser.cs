using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using AngleSharp.Text;
using Correios.NET.Exceptions;
using Correios.NET.Extensions;
using Correios.NET.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;


namespace Correios.NET
{
    public class Parser
    {
        #region Address

        /// <summary>
        /// Parse and converts the html page in a zip address
        /// </summary>
        /// <param name="html">html page</param>
        /// <returns>An Address</returns>
        /// <exception cref="Correios.NET.Exceptions.ParseException"></exception>
        public static IEnumerable<Address> ParseAddresses(string html)
        {
            var document = new HtmlParser().ParseDocument(html);

            var content = document.QuerySelector("div.ctrlcontent");
            var responseText = content.QuerySelector("p").Text();

            if (responseText == "DADOS NAO ENCONTRADOS")
                throw new ParseException("Endereço não encontrado.");

            var list = new List<Address>();

            var tableRows = content.QuerySelectorAll("> table.tmptabela > tbody > tr").Skip(1);

            if (tableRows.Count() == 0)
                throw new ParseException("Endereço não encontrado.");

            foreach (var row in tableRows)
            {
                var address = row.Children;
                var street = address[0].Text().RemoveLineEndings();
                var district = address[1].Text().RemoveLineEndings();
                var cityState = address[2].Text().RemoveLineEndings().Split(new[] { '/' });

                if (cityState.Length != 2)
                    throw new ParseException("Não foi possível extrair as informações de Cidade e Estado.");

                var city = cityState[0].Trim();
                var state = cityState[1].Trim();
                var zipcode = address[3].Text().RemoveHyphens();

                list.Add(new Address
                {
                    Street = street,
                    ZipCode = zipcode,
                    District = district,
                    City = city,
                    State = state
                });
            }

            return list;
        }

        #endregion

        #region Package

        /// <summary>
        /// Parse and converts the html page in a package
        /// </summary>
        /// <param name="html">html page</param>
        /// <returns>A Package</returns>
        /// <exception cref="Correios.NET.Exceptions.ParseException"></exception>
        public static Package ParsePackage(string html)
        {
            try
            {
                var document = new HtmlParser().ParseDocument(html);
                var packageCode = ParsePackageCode(document);
                var package = new Package(packageCode);
                package.AddTrackingInfo(ParsePackageTracking(document));
                return package;
            }
            catch (ParseException ex)
            {
                throw ex;
            }
            catch (Exception ex)
            {
                throw new ParseException("Não foi possível converter o pacote/encomenda.", ex);
            }
        }

        private static string ParsePackageCode(IHtmlDocument document)
        {
            try
            {
                var code = document.QuerySelector(".codSro").Text();

                if (string.IsNullOrEmpty(code))
                    throw new ParseException("Código da encomenda/pacote não foi encontrado.");

                return code;
            }
            catch (ParseException ex)
            {
                throw ex;
            }
            catch (Exception)
            {
                throw new ParseException("Código da encomenda/pacote não foi encontrado.");
            }
        }

        private static IEnumerable<PackageTracking> ParsePackageTracking(IHtmlDocument document)
        {
            var tracking = new List<PackageTracking>();

            PackageTracking status = null;
            var tableRows = document.QuerySelectorAll("table.listEvent.sro tbody tr");

            if (tableRows.Length == 0)
                throw new ParseException("Postagem não encontrada e/ou Aguardando postagem pelo remetente.");

            try
            {
                foreach (var columns in tableRows.Select(tr => tr.Children))
                {
                    if (columns.Count() == 2)
                    {
                        status = new PackageTracking();

                        var dateLocation = columns[0].Text().RemoveLineEndings();
                        var dateLocationSplitted = dateLocation.SplitSpaces();
                        status.Date = DateTime.Parse($"{dateLocationSplitted[0]} {dateLocationSplitted[1]}", CultureInfo.GetCultureInfo("pt-BR"));
                        status.Location = string.Join(" ", dateLocationSplitted.Skip(2).ToArray());
                        status.Status = columns[1].QuerySelector("strong").Text().RemoveLineEndings();

                        var descriptionSplitted = columns[1].Text().RemoveLineEndings().SplitSpaces(3);
                        if (descriptionSplitted.Length > 1)
                            status.Details = string.Join(" ", descriptionSplitted.Skip(1).ToArray());

                        tracking.Add(status);
                    }
                    else
                    {
                        if (status != null)
                            status.Details = columns[0].Text().RemoveLineEndings();
                    }
                }
            }
            catch (Exception ex)
            {
                throw new ParseException("Não foi possível converter o pacote/encomenda.", ex);
            }

            if (tracking.Count() == 0)
                throw new ParseException("Rastreamento não encontrado.");

            return tracking;
        }

        #endregion

        #region Delivery Prices


        public static DeliveryPrice ParseDeliveryPrices(string mode, string html)
        {
            var document = new HtmlParser().ParseDocument(html);
            var content = document.QuerySelector("div.ctrlcontent");
            var error = document.QuerySelectorAll(".info.error").ToList();

            if (error.Count > 0)
                throw new ParseException("Não foi possível calcular o preço de entrega.", error.Select(x => x.Text()).ToArray());

            var tableCols = content.QuerySelectorAll("table.comparaResult tr.destaque td").ToList();
            var termText = tableCols[0].Text();
            var priceText = tableCols[1].Text();

            var rgx = new Regex(@"\d+");
            var match = rgx.Match(termText);
            decimal.TryParse(priceText, NumberStyles.Currency, new CultureInfo("pt-BR"), out decimal price);

            var originDestinationCols = content.QuerySelectorAll("div.contentexpodados > table.comparaResult tr > td").ToList();

            var originAddress = new Address
            {
                ZipCode = originDestinationCols[2].Text(),
                Street = originDestinationCols[4].Text(),
                District = originDestinationCols[6].Text(),
                City = originDestinationCols[8].Text().Split('/')[0].Trim(),
                State = originDestinationCols[8].Text().Split('/')[1].Trim(),
            };

            var destinationAddress = new Address
            {
                ZipCode = originDestinationCols[3].Text(),
                Street = originDestinationCols[5].Text(),
                District = originDestinationCols[7].Text(),
                City = originDestinationCols[9].Text().Split('/')[0].Trim(),
                State = originDestinationCols[9].Text().Split('/')[1].Trim(),
            };

            var deliveryPrice = new DeliveryPrice
            {
                Mode = mode,
                Days = match.Success ? int.Parse(match.Value) : -1,
                Price = price,
                Origin = originAddress,
                Destination = destinationAddress
            };

            return deliveryPrice;
        }

        #endregion
    }
}
