using System;
using System.Globalization;
using System.IO;

namespace DDTImport
{
    public class DocumentoToImport
    {
        public string Fornitore_AgileID { get; set; }
        public string Fornitore_AgileDesc { get; set; }
        public string FornitoreCodice { get; set; }
        public string FornitoreDescrizione { get; set; }
        public string FornitorePIVA { get; set; }
        public string FornitoreCodiceFiscale { get; set; }

        public string Cliente_CodiceAssegnatoDalFornitore { get; set; }
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

        public List<RigaDet> RigheDelDoc { get; set; } = new List<RigaDet>();
    }

    public class RigaDet
    {
        public int RigaNumero { get; set; }
        public string RigaTipo { get; set; }
        public string ArticoloCodiceGenerico { get; set; }
        public string ArticoloCodiceFornitore { get; set; }
        public string ArticoloCodiceProduttore { get; set; }
        public string ArticoloMarca { get; set; }
        public string ArticoloDescrizione { get; set; }
        public string ArticoloBarcode { get; set; }
        public string Articolo_AgileID { get; set; }
        public decimal Qta { get; set; }
        public string UM { get; set; }
        public string Confezione { get; set; }
        public decimal? PrezzoUnitario { get; set; }
        public decimal? Sconto1 { get; set; }
        public decimal? Sconto2 { get; set; }
        public decimal? Sconto3 { get; set; }
        public decimal? PrezzoTotale { get; set; }
        public decimal? PrezzoTotaleScontato { get; set; }
        public string IVACodice { get; set; }
        public decimal IVAAliquota { get; set; }
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
                { "Wuerth", ReadDDT_from_Wuerth },
                { "Spazio", ReadDDT_from_SPAZIO },
                { "Svai", ReadDDT_from_SVAI }
            };
        }

        // Modifica al metodo ReadDDT nella classe DR_Contab_ImportDDT
        public DocumentoToImport ReadDDT(string fileName, string text, string formatoDelTracciato = null)
        {
            Console.WriteLine($"\nInizio elaborazione {fileName}...");

            if (string.IsNullOrEmpty(formatoDelTracciato))
            {
                Console.WriteLine("Rilevamento automatico formato...");
                formatoDelTracciato = DeterminaFormatoTracciato(fileName);
                Console.WriteLine($"Formato rilevato: {formatoDelTracciato}");
            }

            var documento = _formatReaders[formatoDelTracciato](text);
            Console.WriteLine($"Parsing completato. Righe elaborate: {documento.RigheDelDoc.Count}");

            return documento;
        }
        
        private string DeterminaFormatoTracciato(string text)
        {
            if (text.Contains("INNERHOFER", StringComparison.OrdinalIgnoreCase))
                return "Innerhofer";
            if (text.Contains("WUERTH", StringComparison.OrdinalIgnoreCase))
                return "Wuerth";
            if (text.Contains("SVAI", StringComparison.OrdinalIgnoreCase))
                return "Svai";
            if (text.Contains("SPAZIO", StringComparison.OrdinalIgnoreCase))
                return "Spazio";

            throw new InvalidOperationException("Impossibile determinare il formato del tracciato");
        }


        private DocumentoToImport ReadDDT_from_Innerhofer(string text)
        {
            // Inizializza il documento con i dati fissi del fornitore
            var documento = new DocumentoToImport
            {
                Fornitore_AgileID = "INNERHOFER",
                FornitoreDescrizione = "Innerhofer",
                DocTipo = "DDT"
            };

            // Splitta il file in righe
            var lines = text.Split('\n');

            // Processa ogni riga saltando l'intestazione
            for (int i = 1; i < lines.Length; i++)
            {
                // Salta le righe vuote
                if (string.IsNullOrWhiteSpace(lines[i])) continue;

                // Splitta la riga nei vari campi usando il separatore ;
                var fields = lines[i].Split(';');

                // Verifica che la riga abbia tutti i campi necessari
                if (fields.Length < 9) continue;

                // Aggiorna i dati di testata solo alla prima riga valida
                if (documento.DocNumero == null)
                {
                    documento.DocNumero = fields[7].Trim();  // Numero bolla
                    documento.DocData = DateTime.Parse(fields[8].Trim());  // Data bolla
                }

                // Crea una nuova riga del DDT
                var riga = new RigaDet
                {
                    RigaNumero = i,
                    ArticoloCodiceFornitore = fields[0].Trim(),    // Codice articolo
                    ArticoloCodiceGenerico = fields[1].Trim(),     // Codice interno
                    ArticoloDescrizione = fields[2].Trim(),        // Descrizione articolo
                    PrezzoUnitario = decimal.Parse(fields[4].Trim(), CultureInfo.InvariantCulture),  // Prezzo unico/netto
                    Qta = decimal.Parse(fields[5].Trim(), CultureInfo.InvariantCulture),             // Quantità
                    PrezzoTotale = decimal.Parse(fields[6].Trim(), CultureInfo.InvariantCulture)     // Prezzo totale
                };

                // Aggiunge la riga al documento
                documento.RigheDelDoc.Add(riga);
            }

            return documento;
        }

        private DocumentoToImport ReadDDT_from_Wuerth(string text)
        {
            // Inizializza documento con dati fissi Wuerth
            var documento = new DocumentoToImport
            {
                Fornitore_AgileID = "WUERTH",
                FornitoreDescrizione = "Wuerth",
                DocTipo = "DDT"
            };

            var lines = text.Split('\n');

            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;

                var fields = lines[i].Split(';');
                if (fields.Length < 40) continue;

                // Aggiorna dati cliente e testata solo alla prima riga valida
                if (documento.DocNumero == null)
                {
                    documento.Cliente_CodiceAssegnatoDalFornitore = fields[0].Trim();  // CODICE_CLIENTE
                    documento.Cliente_AgileDesc = fields[1].Trim();                     // NOME_CLIENTE
                    documento.DestinazioneMerce1 = fields[2].Trim();                   // VIA
                    documento.DestinazioneMerce2 = $"{fields[3]} {fields[4]} ({fields[5]})"; // CODICE_POSTALE + CITTA + PROVINCIA
                    documento.DocData = DateTime.Parse(fields[7].Trim());              // DATA_DDT
                    documento.DocNumero = fields[8].Trim();                           // NUMERO_DDT
                }

                var riga = new RigaDet
                {
                    RigaNumero = int.Parse(fields[9].Trim()),                         // NUMERO_POS_DDT
                    ArticoloCodiceFornitore = fields[10].Trim(),                     // CODICE_PRODOTTO
                    ArticoloDescrizione = fields[11].Trim(),                         // DESCRIZIONE_PRODOTTO
                    Confezione = fields[12].Trim(),                                  // CONFEZIONE
                    RifOrdineCliente = fields[14].Trim(),                           // NUMERO_ORDINE_CLIENTE
                    ArticoloCodiceGenerico = fields[16].Trim(),                     // CODICE_ARTICOLO_CLIENTE
                    UM = fields[17].Trim(),                                         // UNITA_DI_MISURA
                    Qta = decimal.Parse(fields[18].Trim(), CultureInfo.InvariantCulture),  // QUANTITA
                    PrezzoUnitario = decimal.Parse(fields[19].Trim(), CultureInfo.InvariantCulture),  // PREZZO_NETTO
                    PrezzoTotale = decimal.Parse(fields[21].Trim(), CultureInfo.InvariantCulture),    // PREZZO_POSIZIONE
                    IVAAliquota = decimal.Parse(fields[23].Trim(), CultureInfo.InvariantCulture),     // ALIQUOTA_IVA
                    RifOrdineFornitore = fields[25].Trim(),                         // NUMERO_ORDINE
                    ArticoloBarcode = fields[27].Trim(),                            // CODICE_EAN
                };

                documento.RigheDelDoc.Add(riga);
            }

            return documento;
        }

        private DocumentoToImport ReadDDT_from_SVAI(string text)
        {
            // Inizializza documento con dati fissi SVAI
            var documento = new DocumentoToImport
            {
                Fornitore_AgileID = "SVAI",
                FornitoreDescrizione = "SVAI Srl",
                DocTipo = "DDT"
            };

            var lines = text.Split('\n');

            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;

                var fields = lines[i].Split(';');
                if (fields.Length < 21) continue;

                // Aggiorna dati testata solo alla prima riga valida
                if (documento.DocNumero == null)
                {
                    documento.DocNumero = fields[0].Trim();                    // Numero_Bolla
                    documento.DocData = DateTime.Parse(fields[1].Trim());      // Data_Bolla
                    documento.Cliente_AgileDesc = fields[2].Trim();           // Rag_Soc_1
                    string ragSoc2 = fields[3].Trim();                       // Rag_Soc_2
                    if (!string.IsNullOrEmpty(ragSoc2))
                    {
                        documento.Cliente_AgileDesc += " " + ragSoc2;
                    }
                    documento.DestinazioneMerce1 = fields[4].Trim();         // Indirizzo
                    documento.DestinazioneMerce2 = $"{fields[5]} {fields[6]} ({fields[7]})"; // CAP + Localita + Provincia
                }

                var riga = new RigaDet
                {
                    RigaTipo = fields[8].Trim(),                            // Tipo_Riga
                    ArticoloCodiceFornitore = fields[9].Trim(),            // Codice_Articolo
                    ArticoloMarca = fields[10].Trim(),                     // Marca
                    ArticoloDescrizione = fields[11].Trim(),               // Descrizione_Articolo
                    ArticoloCodiceGenerico = fields[12].Trim(),            // Codice Fornitore
                    Qta = decimal.Parse(fields[13].Trim(), CultureInfo.InvariantCulture),    // Quantita
                    PrezzoUnitario = decimal.Parse(fields[14].Trim(), CultureInfo.InvariantCulture), // Prezzo
                    Sconto1 = decimal.Parse(fields[15].Trim(), CultureInfo.InvariantCulture), // Sconto_1
                    Sconto2 = decimal.Parse(fields[16].Trim(), CultureInfo.InvariantCulture), // Sconto_2
                    Sconto3 = decimal.Parse(fields[17].Trim(), CultureInfo.InvariantCulture), // Sconto_3
                    PrezzoTotaleScontato = decimal.Parse(fields[18].Trim(), CultureInfo.InvariantCulture), // Netto_Riga
                    IVAAliquota = decimal.Parse(fields[19].Trim(), CultureInfo.InvariantCulture), // IVA
                    RifOrdineFornitore = fields[20].Trim()                 // Ordine
                };

                documento.RigheDelDoc.Add(riga);
            }

            return documento;
        }

        private DocumentoToImport ReadDDT_from_SPAZIO(string text)
        {
            // Implementazione placeholder per Wuerth
            var documento = new DocumentoToImport
            {
                Fornitore_AgileID = "SPAZIO",
                DocTipo = "DDT"
                // Implementare la logica di parsing specifica per Wuerth
            };

            return documento;
        }
    }


    public class Program
    {
        public static void Main()
        {
            Console.WriteLine("=== IMPORT DDT ===");
            var reader = new DR_Contab_ImportDDT();

            while (true)
            {
                Console.WriteLine("\nInserisci il percorso del file DDT (o 'exit' per uscire):");
                string filePath = Console.ReadLine();

                if (filePath?.ToLower() == "exit")
                    break;

                if (!File.Exists(filePath))
                {
                    Console.WriteLine("File non trovato. Riprova.");
                    continue;
                }

                try
                {
                    string fileName = Path.GetFileName(filePath);
                    Console.WriteLine($"\nElaborazione del file: {fileName}");

                    Console.WriteLine("\nScegli il formato del file:");
                    Console.WriteLine("1. Innerhofer");
                    Console.WriteLine("2. Wuerth");
                    Console.WriteLine("3. Spazio");
                    Console.WriteLine("4. Wuerth");
                    Console.WriteLine("5. Rilevamento automatico");

                    string scelta = Console.ReadLine();
                    string contenutoFile = File.ReadAllText(filePath);
                    DocumentoToImport documento;

                    switch (scelta)
                    {
                        case "1":
                            documento = reader.ReadDDT(fileName, contenutoFile, "Innerhofer");
                            break;
                        case "2":
                            documento = reader.ReadDDT(fileName, contenutoFile, "Wuerth");
                            break;
                        case "3":
                            documento = reader.ReadDDT(fileName, contenutoFile, "Spazio");
                            break;
                        case "4":
                            documento = reader.ReadDDT(fileName, contenutoFile, "Svai");
                            break;
                        case "5":
                            documento = reader.ReadDDT(fileName, contenutoFile);
                            break;
                        default:
                            Console.WriteLine("Scelta non valida.");
                            continue;
                    }

                    WriteDDTToFile(documento);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Errore: {ex.Message}");
                }
            }
        }

        public static void WriteDDTToFile(DocumentoToImport doc)
        {
            string outputPath = "output.txt";
            using (StreamWriter writer = new StreamWriter(outputPath))
            {
                // Scrivi intestazione documento
                writer.WriteLine("=== DATI DOCUMENTO ===");
                writer.WriteLine($"Fornitore: {doc.FornitoreDescrizione}");
                writer.WriteLine($"Tipo Documento: {doc.DocTipo}");
                writer.WriteLine($"Numero: {doc.DocNumero}");
                writer.WriteLine($"Data: {doc.DocData:dd/MM/yyyy}");
                writer.WriteLine($"Cliente: {doc.Cliente_AgileDesc}");
                writer.WriteLine($"Destinazione: {doc.DestinazioneMerce1}");
                writer.WriteLine($"             {doc.DestinazioneMerce2}");

                // Scrivi prima riga del documento
                if (doc.RigheDelDoc.Count > 0)
                {
                    var primaRiga = doc.RigheDelDoc[0];
                    writer.WriteLine("\n=== PRIMA RIGA ===");
                    writer.WriteLine($"Codice: {primaRiga.ArticoloCodiceFornitore}");
                    writer.WriteLine($"Descrizione: {primaRiga.ArticoloDescrizione}");
                    writer.WriteLine($"Quantità: {primaRiga.Qta}");
                    writer.WriteLine($"Prezzo Unitario: {primaRiga.PrezzoUnitario:C2}");
                    writer.WriteLine($"Prezzo Totale: {primaRiga.PrezzoTotale:C2}");
                }

                writer.WriteLine($"\nTotale righe nel documento: {doc.RigheDelDoc.Count}");
            }
        }
    } //C:\Users\kevin\OneDrive\Documenti\lavoro\Import DDT\Innerhofer DDT 23-24.csv
}

