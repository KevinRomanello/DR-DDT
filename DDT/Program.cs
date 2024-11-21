using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace DDTImport
{
    public class DocumentoToImport
    {
        public string Fornitore_AgileID { get; set; }
        public string Fornitore_AgileDesc { get; set; }
        public string FornitoreCodice { get; set; }   // nome fornitore
        public string FornitoreDescrizione { get; set; }
        public string FornitorePIVA { get; set; }
        public string FornitoreCodiceFiscale { get; set; }

        public string Cliente_CodiceAssegnatoDalFornitore { get; set; }
        public string Cliente { get; set; }   // CAMPO DOVE MEMORIZZO IL CLIENTE
        public string Cliente_AgileID { get; set; }
        public string Cliente_AgileDesc { get; set; }
        public string Cliente_PIVA { get; set; }
        public string Cliente_CodiceFiscale { get; set; }

        public string DocTipo { get; set; }
        public string DocNumero { get; set; }
        public DateTime DocData { get; set; }
        public string DocBarcode { get; set; }
        public string RifOrdineFornitore { get; set; }
        public string RifOrdineCliente { get; set; }
        public string DestinazioneMerce1 { get; set; }
        public string DestinazioneMerce2 { get; set; }
        public string Note { get; set; }

        public string TrasportoData { get; set; }
        public string TrasportoNote { get; set; }
        public String Verificato { get; set; }
        public List<RigaDet> RigheDelDoc { get; set; } = new List<RigaDet>();
    }

    public class RigaDet
    {
        public int RigaNumero { get; set; }
        public string RigaTipo { get; set; }
        public string NumeroDoc { get; set; }
        public string CodiceCliente { get; set; }
        public DateTime? DataOrdine { get; set; }
        public string ArticoloCodiceGenerico { get; set; }
        public string ArticoloCodiceFornitore { get; set; }
        public string ArticoloCodiceProduttore { get; set; }
        public string ArticoloMarca { get; set; }
        public string ArticoloDescrizione { get; set; }
        public string ArticoloBarcode { get; set; }
        public string Articolo_AgileID { get; set; }
        public decimal? Qta { get; set; }
        public string UM { get; set; }
        public string Confezione { get; set; }
        public decimal? PrezzoUnitario { get; set; }
        public decimal? Sconto1 { get; set; }
        public decimal? Sconto2 { get; set; }
        public decimal? Sconto3 { get; set; }
        public decimal? PrezzoTotale { get; set; }
        public decimal? PrezzoTotaleScontato { get; set; }
        public string IVACodice { get; set; }
        public decimal? IVAAliquota { get; set; }
        public string RifOrdineFornitore { get; set; }
        public string RifOrdineCliente { get; set; }
        public string DestinazioneMerce { get; set; }
    }

    public class DR_Contab_ImportDDT
    {
        private readonly Dictionary<string, Func<string, DocumentoToImport>> _formatReaders;

        public DR_Contab_ImportDDT()
        {
            _formatReaders = new Dictionary<string, Func<string, DocumentoToImport>>
            {
                { "Innerhofer", ReadDDT_from_Innerhofer },
                { "Wurth", ReadDDT_from_Wurth },
                { "Spazio", ReadDDT_from_Spazio },
                { "Svai", ReadDDT_from_SVAI }
            };
        }


        public DocumentoToImport ReadDDT(string fileName, string text, string formatoDelTracciato = null)
        {
            try
            {
                // Se non è specificato il formato, lo rileva automaticamente
                if (string.IsNullOrEmpty(formatoDelTracciato))
                {
                    formatoDelTracciato = DeterminaFormatoTracciato(text);
                }

                // Usa il formato per selezionare il parser appropriato
                var documento = _formatReaders[formatoDelTracciato](text);

                VerificaDestinatario(documento);

                // Verifica che ci siano righe
                if (documento.RigheDelDoc.Count == 0)
                {
                    throw new InvalidOperationException("Nessuna riga trovata nel documento");
                }

                return documento;
            }
            catch (KeyNotFoundException)
            {
                throw new InvalidOperationException("Formato del tracciato non supportato");
            }
            catch (Exception ex)
            {
                throw new Exception($"Errore nell'elaborazione del DDT: {ex.Message}");
            }
        }


        // Funazione che determina il formato del DDT tramite l'inetstazione
        private string DeterminaFormatoTracciato(string text)
        {
            try
            {
                // Dividiamo il testo in righe
                var lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                var firstLine = lines.FirstOrDefault();

                // Se non ci sono righe, il file non è valido
                if (string.IsNullOrWhiteSpace(firstLine))
                    throw new InvalidOperationException("File vuoto o non valido");

                // Controlliamo se la riga usa il separatore ';' o uno spazio
                bool usesSemicolon = firstLine.Contains(';');

                // Normalizziamo la prima riga
                var headerLine = CleanText(firstLine);

                // Dividiamo le intestazioni in base al separatore
                var headers = SplitHeaders(headerLine, usesSemicolon);

                // Se non ci sono intestazioni, il file non è valido
                if (headers == null || headers.Count == 0)
                    throw new InvalidOperationException("File vuoto o non valido");

                // Definiamo le intestazioni attese per ciascun fornitore
                var formatHeaders = new Dictionary<string, IEnumerable<string>>()
                {
                    { "Wurth", new[]
                        {
                            "CODICE_CLIENTE", "NOME_CLIENTE", "VIA", "CODICE_POSTALE", "CITTA", "PROVINCIA",
                            "PAESE", "DATA_DDT", "NUMERO_DDT",
                            "NUMERO_POS_DDT", "CODICE_PRODOTTO", "DESCRIZIONE_PRODOTTO", "CONFEZIONE",
                            "DATA_ORDINE_CLIENTE", "NUMERO_ORDINE_CLIENTE", "NUMERO_POS_ORDINE_CLIENTE",
                            "CODICE_ARTICOLO_CLIENTE", "UNITA_DI_MISURA", "QUANTITA", "PREZZO_NETTO",
                            "UNITA_PREZZO", "PREZZO_POSIZIONE", "TOTALE_IVA", "ALIQUOTA_IVA", "DATA_ORDINE",
                            "NUMERO_ORDINE", "NUMERO_POSIZIONE_ORDINE", "CODICE_EAN", "CODICE_DEALER_COMMITTENTE",
                            "CODICE_DEALER_DESTINATARIO_MERCI", "DISPONENT", "CODICE_MERCEOLOGICO",
                            "STATO_ORIGINE_MERCE", "LETTERA_DI_VETTURA", "CENTRO_DI_COSTO", "NOTA", "LOTTO",
                            "AVVISO_DI_CONSEGNA", "NUMERO_SERIALE"
                        }
                    },
                    { "Svai", new[]
                        {
                            "Numero_Bolla", "Data_Bolla", "Rag_Soc_1", "Rag_Soc_2", "Indirizzo",
                            "CAP", "Localita", "Provincia","Tipo_Riga","Codice_Articolo","Marca","Descrizione_Articolo","Codice Fornitore",
                            "Quantita","Prezzo","Sconto_1","Sconto_2","Sconto_3", "Netto_Riga","IVA","Ordine"
                        }
                    },
                    { "Spazio", new[] { "CODICE ARTICOLO","DESCRIZIONE", "QTA","IM. UNI. NETTO","Prezzo netto Tot.","al. iva", "N� ORDINE" } }
                };

                // Funzione per verificare se tutte le intestazioni attese sono presenti
                bool MatchesHeaders(IEnumerable<string> expectedHeaders)
                {
                    return expectedHeaders.All(expected => headers.Any(h => h.Equals(expected, StringComparison.OrdinalIgnoreCase)));
                }

                // Se il file non usa il separatore ';', consideriamo Innerhofer
                if (!usesSemicolon)
                {
                    return "Innerhofer";
                }

                // Verifica se corrisponde a uno dei formati definiti
                foreach (var format in formatHeaders)
                {
                    if (MatchesHeaders(format.Value))
                        return format.Key;
                }

                throw new InvalidOperationException("Impossibile determinare il formato del tracciato");
            }
            catch (Exception ex)
            {
                throw new Exception(($"\nErrore nel determinare il formato: {ex.Message}"));
            }
        }

        private List<string> SplitHeaders(string headerLine, bool usesSemicolon)
        {
            return usesSemicolon
                ? headerLine.Split(';')
                    .Select(h => new string(h.Where(c => !char.IsControl(c)).ToArray()).Trim())
                    .ToList()
                : headerLine.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(h => h.Trim())
                    .ToList();
        }



        private void VerificaDestinatario(DocumentoToImport documento)
        {
            // Verifica se il destinatario è ERREBI
            if (documento.Cliente != null && documento.Cliente.Contains("ERRE-BI", StringComparison.OrdinalIgnoreCase))
            {
                documento.Verificato = "Verificato";
            }
            else
            {
                documento.Verificato = "Non Verificato";
            }
        }


        private DocumentoToImport ReadDDT_from_Innerhofer(string text)
        {
            var documento = new DocumentoToImport
            {
                Fornitore_AgileID = "INNERHOFER",
                FornitoreDescrizione = "INNERHOFER",
                DocTipo = "DDT",
                RigheDelDoc = new List<RigaDet>()
            };

            int i = 0;
            foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                i++;
                try
                {

                    if (line.StartsWith("TADS"))
                    {
                        documento.DocNumero = line.Substring(25, 9).Trim().TrimStart('0');
                        documento.DocData = DateTime.ParseExact(line.Substring(40, 8).Trim(), "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None);
                        documento.Cliente = line.Substring(60, 50).Trim();
                        documento.DestinazioneMerce1 = line.Substring(99, 120).Trim();
                    }
                    else if (line.StartsWith("RADS"))
                    {
                        int orderPos = line.IndexOf("ORD");
                        var riga = new RigaDet
                        {
                            RigaTipo = "RADS",
                            ArticoloCodiceFornitore = line.Substring(54, 12).Trim(),
                            ArticoloCodiceProduttore = line.Substring(69, 17).Trim(),
                            ArticoloDescrizione = line.Substring(97, 40).Trim(),
                            UM = line.Substring(140, 12).Trim(),
                            RifOrdineCliente = line.Substring(orderPos, 10).Trim(),   // offest per trovare la data da ORD
                            DataOrdine = DateTime.ParseExact(line.Substring(orderPos + 15, 8).Trim(), "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None)
                        };
                        documento.RigheDelDoc.Add(riga);
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"Errore nel parsing della riga {i}: {ex.Message}");
                }
            }

            return documento;
        }


        private DocumentoToImport ReadDDT_from_Wurth(string text)
        {
            var documento = new DocumentoToImport
            {
                Fornitore_AgileID = "WURTH",
                FornitoreDescrizione = "Wurth",
                DocTipo = "DDT",
                RigheDelDoc = new List<RigaDet>()
            };

            // Legge le righe del CSV
            var lines = text.Split('\n');
            if (lines.Length < 2) return documento; // Verifica che ci sia almeno l'header e una riga

            // Legge l'header e mappa gli indici delle colonne
            var headerLine = lines[0];
            var headers = headerLine.Split(';');

            var columnIndexes = new Dictionary<string, int>();
            for (int i = 0; i < headers.Length; i++)
            {
                columnIndexes[headers[i].Trim()] = i;
            }

            // Processa le righe di dati
            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                var values = line.Split(';');

                try
                {
                    // Per la prima riga, imposta i dati del documento
                    if (documento.DocNumero == null)
                    {
                        documento.DocNumero = values[columnIndexes["NUMERO_DDT"]].Trim(); // NUMERO_DDT

                        if (DateTime.TryParseExact(values[columnIndexes["DATA_DDT"]].Trim(), "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime docData))
                        {
                            documento.DocData = docData;
                        }

                        documento.Cliente_CodiceAssegnatoDalFornitore = values[columnIndexes["CODICE_CLIENTE"]].Trim();
                        documento.Cliente = values[columnIndexes["NOME_CLIENTE"]].Trim();

                        // Formatta l'indirizzo correttamente
                        var via = values[columnIndexes["VIA"]].Trim();
                        var cap = values[columnIndexes["CODICE_POSTALE"]].Trim();
                        var citta = values[columnIndexes["CITTA"]].Trim();
                        var provincia = values[columnIndexes["PROVINCIA"]].Trim();
                        var paese = values[columnIndexes["PAESE"]].Trim();
                        documento.DestinazioneMerce2 = $"{via}\n{cap} {citta} ({provincia})";

                        documento.RifOrdineCliente = values[columnIndexes["NUMERO_ORDINE_CLIENTE"]].Trim();
                    }

                    // Crea una nuova riga
                    var riga = new RigaDet
                    {
                        RigaNumero = int.Parse(values[columnIndexes["NUMERO_POS_DDT"]].Trim().TrimStart('0')), // NUMERO_POS_DDT senza gli zero iniziali
                        ArticoloCodiceFornitore = values[columnIndexes["CODICE_PRODOTTO"]].Trim(),           // CODICE_PRODOTTO
                        ArticoloDescrizione = values[columnIndexes["DESCRIZIONE_PRODOTTO"]].Trim(),         // DESCRIZIONE_PRODOTTO
                        Confezione = values[columnIndexes["CONFEZIONE"]].Trim(),                            // CONFEZIONE
                        UM = values[columnIndexes["UNITA_DI_MISURA"]].Trim(),                               // UNITA_DI_MISURA
                        Qta = ParseNullableDecimal(values[columnIndexes["QUANTITA"]].Trim()), // QUANTITA
                        ArticoloBarcode = values[columnIndexes["CODICE_EAN"]].Trim(),                      // CODICE_EAN
                        ArticoloCodiceGenerico = values[columnIndexes["CODICE_MERCEOLOGICO"]].Trim(),            // CODICE_MERCEOLOGICO -> ArticoloCodiceGenerico
                        DataOrdine = DateTime.ParseExact(values[columnIndexes["DATA_ORDINE_CLIENTE"]].Trim(), "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None), // data ordine

                        PrezzoUnitario = ParseNullableDecimal(values[columnIndexes["PREZZO_NETTO"]].Trim()), // PREZZO_NETTO
                        PrezzoTotale = ParseNullableDecimal(values[columnIndexes["PREZZO_POSIZIONE"]].Trim()), // PREZZO_POSIZIONE

                        IVAAliquota = ParseNullableDecimal(values[columnIndexes["ALIQUOTA_IVA"]].Trim()),  // ALIQUOTA_IVA

                        RifOrdineFornitore = values[columnIndexes["NUMERO_ORDINE"]].Trim()                // NUMERO_ORDINE
                    };

                    // Se c'è un prezzo unitario, calcola il prezzo totale scontato
                    if (riga.PrezzoUnitario.HasValue)
                    {
                        riga.PrezzoTotaleScontato = riga.PrezzoTotale;
                    }

                    documento.RigheDelDoc.Add(riga);

                }
                catch (Exception ex)
                {
                    throw new Exception($"Errore nel parsing della riga {i + 1}: {ex.Message}");
                }
            }

            return documento;
        }

        // parsing pe rl agestione di , e .
        private decimal? ParseNullableDecimal(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;

            return decimal.TryParse(value,
                NumberStyles.Any,
                CultureInfo.GetCultureInfo("it-IT"),
                out decimal result) ? result : null;
        }


        private DocumentoToImport ReadDDT_from_SVAI(string text)
        {

            var documento = new DocumentoToImport
            {
                Fornitore_AgileID = "SVAI",
                FornitoreDescrizione = "SVAI",
                DocTipo = "DDT",
                RigheDelDoc = new List<RigaDet>()
            };

            var lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 2)
                throw new InvalidOperationException("Il file non contiene dati sufficienti");

            var headers = lines[0].Split(';');
            var columnIndexes = new Dictionary<string, int>();

            // Mappiamo gli indici delle colonne in base all'intestazione
            for (int i = 0; i < headers.Length; i++)
            {
                columnIndexes[headers[i].Trim()] = i;
            }

            bool firstRow = true;  // Flag per la prima riga
            int rigaNum = 1;  // Numero riga per le righe del documento

            for (int i = 1; i < lines.Length; i++)
            {
                var fields = lines[i].Split(';');

                // Saltiamo righe malformate o con un numero di campi insufficiente
                if (fields.Length < headers.Length) continue;

                try
                {
                    // Elaboriamo solo la prima riga per i dati del documento
                    if (firstRow)
                    {
                        documento.DocNumero = fields[columnIndexes["Numero_Bolla"]].Trim();

                        // Parsing della data con gestione di errori
                        if (DateTime.TryParseExact(fields[columnIndexes["Data_Bolla"]].Trim(), "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime docData))
                        {
                            documento.DocData = docData;
                        }
                        else
                        {
                            Console.WriteLine($"Data non valida per la riga {i + 1}. Utilizzo valore predefinito.");
                        }

                        firstRow = false;  // Impostiamo il flag per non elaborare più la prima riga
                    }

                    // Creazione e popolazione delle righe del documento
                    var riga = new RigaDet
                    {
                        RigaNumero = rigaNum++,
                        RigaTipo = fields[columnIndexes["Tipo_Riga"]].Trim(),
                        ArticoloCodiceFornitore = fields[columnIndexes["Codice Fornitore"]].Trim(),
                        ArticoloCodiceGenerico = fields[columnIndexes["Codice_Articolo"]].Trim(),
                        ArticoloMarca = fields[columnIndexes["Marca"]].Trim(),
                        ArticoloDescrizione = fields[columnIndexes["Descrizione_Articolo"]].Trim(),
                        Qta = ParseNullableDecimal(fields[columnIndexes["Quantita"]]),
                        PrezzoUnitario = ParseNullableDecimal(fields[columnIndexes["Prezzo"]]),
                        PrezzoTotale = ParseNullableDecimal(fields[columnIndexes["Netto_Riga"]]),
                        IVAAliquota = ParseNullableDecimal(fields[columnIndexes["IVA"]]),
                        Sconto1 = ParseNullableDecimal(fields[columnIndexes["Sconto_1"]]),
                        Sconto2 = ParseNullableDecimal(fields[columnIndexes["Sconto_2"]]),
                        Sconto3 = ParseNullableDecimal(fields[columnIndexes["Sconto_3"]]),
                        RifOrdineFornitore = fields[columnIndexes["Ordine"]].Trim(),
                    };

                    documento.RigheDelDoc.Add(riga);
                }
                catch (Exception ex)
                {
                    throw new Exception($"Errore nel parsing della riga {i + 1}: {ex.Message}");
                }
            }

            // Verifica che ci siano righe valide
            if (documento.RigheDelDoc.Count == 0)
            {
                throw new InvalidOperationException("Nessuna riga valida trovata nel documento");
            }

            return documento;
        }


        private DocumentoToImport ReadDDT_from_Spazio(string text)
        {

            // Inizializza il documento
            var documento = new DocumentoToImport
            {
                Fornitore_AgileID = "SPAZIO",
                FornitoreDescrizione = "SPAZIO",
                DocTipo = "DDT",
                RigheDelDoc = new List<RigaDet>()
            };

            // Separa il testo in righe e rimuove righe vuote
            var lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            // Verifica che il file abbia almeno due righe
            if (lines.Length < 2)
                throw new InvalidOperationException("Il file non contiene dati sufficienti");

            // Imposta la data di oggi come data documento, se non presente
            documento.DocData = DateTime.Today;

            // Elimina caratteri indesiderati dall'intestazione
            var headerLine = CleanText(lines[0]); // Rimuove caratteri non stampabili (ad esempio &nbsp;)

            // Crea una lista di intestazioni di colonne, rimuovendo i caratteri di controllo
            var headers = headerLine.Split(';')
                .Select(h => new string(h.Where(c => !char.IsControl(c)).ToArray())) // Rimuove caratteri di controllo
                .Select(h => h.Trim()) // Rimuove spazi all'inizio e alla fine
                .ToList();

            // Mappa le intestazioni alle loro posizioni
            var columnIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < headers.Count; i++)
            {
                var cleanHeader = headers[i].Trim();
                columnIndexes[cleanHeader] = i;
            }

            int rigaNum = 1;  // Numero progressivo della riga

            // Processa tutte le righe del documento
            for (int i = 1; i < lines.Length; i++)
            {
                try
                {
                    // Rimuove caratteri indesiderati dalla riga
                    var line = CleanText(lines[i]);  // Rimuove caratteri non stampabili (ad esempio &nbsp;)

                    var fields = line.Split(';')
                        .Select(f => f.Trim())  // Rimuove spazi extra da ogni campo
                        .ToArray();

                    // Se la riga non ha un numero sufficiente di campi, la saltiamo
                    if (fields.Length < headers.Count) continue;

                    // Estrae e pulisce i campi necessari
                    string codiceArticolo = fields[columnIndexes["CODICE ARTICOLO"]]
                        .Replace(" ", "")  // Rimuove tutti gli spazi
                        .Trim();

                    string descrizione = fields[columnIndexes["DESCRIZIONE"]]
                        .Replace("  ", " ")  // Rimuove spazi doppi
                        .Trim();

                    // Crea una riga di documento
                    var riga = new RigaDet
                    {
                        RigaNumero = rigaNum++,  // Incrementa il numero della riga
                        ArticoloCodiceFornitore = codiceArticolo,
                        ArticoloDescrizione = descrizione,
                        Qta = ParseNullableDecimal(fields[columnIndexes["QTA"]]),
                        PrezzoUnitario = ParseNullableDecimal(fields[columnIndexes["IM. UNI. NETTO"]]),
                        PrezzoTotale = ParseNullableDecimal(fields[columnIndexes["Prezzo netto Tot."]]),
                        IVAAliquota = ParseNullableDecimal(fields[columnIndexes["al. iva"]])
                    };

                    // Aggiunge la riga al documento
                    documento.RigheDelDoc.Add(riga);
                }
                catch (Exception ex)
                {
                    throw new Exception($"Errore nel parsing della riga {i + 1}: {ex.Message}");
                }
            }

            // Se non ci sono righe valide, lancia un'eccezione
            if (documento.RigheDelDoc.Count == 0)
            {
                throw new InvalidOperationException("Nessuna riga valida trovata nel documento");
            }

            return documento;
        }

        private string CleanText(string input)
        {
            return input
                .Replace("\"", "")  // Rimuove le virgolette
                .Replace("\t", "")  // Rimuove i tabulati
                .Replace("\u00A0", " ")  // Rimuove il carattere &nbsp; (spazio non separabile)
                .Trim();  // Rimuove gli spazi all'inizio e alla fine
        }

    }
}

    








        