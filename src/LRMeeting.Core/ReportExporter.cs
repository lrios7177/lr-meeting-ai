using System.Net;
using System.Text;
using System.Text.Json;
namespace LRMeeting.Core;

public static class ReportExporter
{
    private static string H(string? text)=>WebUtility.HtmlEncode(text ?? "Por confirmar");
    public static async Task ExportAsync(Meeting meeting,ExecutiveReport report,IReadOnlyList<TranscriptSegment> segments,CancellationToken cancellationToken)
    {
        var html=new StringBuilder("""
        <!doctype html><html lang="es"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Informe ejecutivo · LR Meeting AI</title>
        <style>
        :root{color-scheme:light;font-family:Segoe UI,Arial,sans-serif;color:#17243b;background:#edf1f7}*{box-sizing:border-box}body{margin:0;padding:40px 20px}.page{max-width:1080px;margin:auto;background:white;border-radius:24px;overflow:hidden;box-shadow:0 18px 60px #17243b12}header{background:#111c31;color:white;padding:44px 48px;border-top:6px solid #35d6bb}.brand{letter-spacing:3px;font-size:12px;color:#88e8d7}h1{font-size:34px;line-height:1.2;margin:18px 0}h2{font-size:22px;margin:30px 0 16px}h3{font-size:16px;margin:0 0 8px}p{line-height:1.7;white-space:pre-wrap}.meta{color:#b7c6db;font-size:13px}.body{padding:12px 48px 40px}.summary{background:#edf9f6;border-left:4px solid #1eaa90;padding:20px 24px;border-radius:8px}.grid{display:grid;grid-template-columns:repeat(3,1fr);gap:12px;margin:24px 0}.stat{background:#f3f5fb;padding:18px;border-radius:12px;color:#52617c}.stat strong{display:block;font-size:28px;color:#17243b}.point{padding:18px 0;border-bottom:1px solid #e9edf4}.refs{font-size:11px;color:#6a7891;overflow-wrap:anywhere}.refs a{color:#375db1}table{width:100%;border-collapse:collapse;font-size:13px}td,th{text-align:left;border-bottom:1px solid #e5eaf2;padding:13px 10px;vertical-align:top}th{background:#f3f5fb}.notice{padding:16px;background:#fff7df;color:#775817;border-radius:10px;font-size:13px}footer{color:#6a7891;font-size:12px;margin-top:36px}.tools{max-width:1080px;margin:0 auto 16px;display:flex;justify-content:flex-end}button{background:#253f7b;color:white;border:0;border-radius:8px;padding:12px 20px;cursor:pointer}details{margin-top:28px}summary{cursor:pointer;font-weight:600}@media(max-width:650px){header,.body{padding:24px}.grid{grid-template-columns:1fr}h1{font-size:26px}body{padding:12px}}@media print{@page{size:A4;margin:15mm}body{padding:0;background:white}.tools{display:none}.page{box-shadow:none;border-radius:0;max-width:none}header{print-color-adjust:exact}.point,tr{break-inside:avoid}details{display:none}}
        </style><div class="tools"><button onclick="window.print()">Imprimir / Guardar PDF</button></div><article class="page"><header><div class="brand">LR MEETING AI · INFORME EJECUTIVO</div><h1>
        """);
        html.Append(H(meeting.Title)).Append("</h1><div class=meta>").Append(H(meeting.CreatedUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm")))
            .Append(" · ").Append(H(report.Status)).Append("</div></header><div class=body><div class=grid>")
            .Append($"<div class=stat><strong>{report.Topics.Length}</strong>Temas tratados</div><div class=stat><strong>{report.Decisions.Length}</strong>Acuerdos identificados</div><div class=stat><strong>{report.Actions.Length}</strong>Tareas pendientes</div></div>")
            .Append("<h2>Resumen ejecutivo</h2><div class=summary><p>").Append(H(report.Summary)).Append("</p></div>");
        string Refs(string[] ids)=>"<div class=refs>Evidencia: "+string.Join(" · ",ids.Select(id=>$"<a href=\"#s-{H(id)}\">{H(id)}</a>"))+"</div>";
        void Points(string title,ReportPoint[] points)
        {
            html.Append("<h2>").Append(H(title)).Append("</h2>");
            if(points.Length==0) html.Append("<p>No se identificaron elementos con evidencia suficiente.</p>");
            foreach(var point in points) html.Append("<section class=point><h3>").Append(H(point.Title)).Append("</h3><p>").Append(H(point.Detail)).Append("</p>").Append(Refs(point.Evidence)).Append("</section>");
        }
        Points("Temas tratados",report.Topics); Points("Decisiones y acuerdos",report.Decisions);
        html.Append("<h2>Plan de acción</h2><table><tr><th>Acción</th><th>Responsable</th><th>Fecha</th></tr>");
        foreach(var action in report.Actions) html.Append("<tr><td>").Append(H(action.Task)).Append(Refs(action.Evidence)).Append("</td><td>").Append(H(action.Owner)).Append("</td><td>").Append(H(action.DueDate)).Append("</td></tr>");
        html.Append("</table>");
        if(report.Actions.Length==0) html.Append("<p>No se identificaron tareas explícitas.</p>");
        Points("Asuntos pendientes y próximos pasos",report.OpenIssues); Points("Riesgos y bloqueos",report.Risks);
        html.Append("<h2>Cobertura y revisión</h2><div class=notice>").Append(H(report.Coverage))
            .Append(" No se garantiza transcripción perfecta. Verifica nombres, cifras, responsables y decisiones antes de compartir.</div><details><summary>Segmentos de referencia</summary><table><tr><th>Tiempo / fuente</th><th>Texto revisable</th></tr>");
        foreach(var segment in segments) html.Append("<tr id=\"s-").Append(H(segment.Id)).Append("\"><td>")
            .Append(H(MeetingTime.Format(segment.StartMs))).Append(" · ").Append(H(segment.Channel)).Append("<br><small>").Append(H(segment.Id))
            .Append("</small></td><td>").Append(H(segment.Text)).Append("</td></tr>");
        html.Append("</table></details><footer>Ing. Luis Ríos, MBA · LR Meeting AI · Procesamiento local<br>Generado ").Append(H(report.GeneratedUtc.ToLocalTime().ToString("g"))).Append("</footer></div></article></html>");
        await AtomicWriteAsync(Path.Combine(meeting.Folder,"report.html"),html.ToString(),cancellationToken).ConfigureAwait(false);
        await AtomicWriteAsync(Path.Combine(meeting.Folder,"report.json"),JsonSerializer.Serialize(report,JsonDefaults.Options),cancellationToken).ConfigureAwait(false);
        await ExportTranscriptAsync(meeting,segments,cancellationToken).ConfigureAwait(false);
    }
    public static async Task ExportTranscriptAsync(Meeting meeting,IReadOnlyList<TranscriptSegment> segments,CancellationToken cancellationToken)
    {
        await AtomicWriteAsync(Path.Combine(meeting.Folder,"transcript.txt"),string.Join(Environment.NewLine,segments.Select(s=>$"[{MeetingTime.Format(s.StartMs)}] [{s.Channel}] {s.Text}")),cancellationToken).ConfigureAwait(false);
        await AtomicWriteAsync(Path.Combine(meeting.Folder,"transcript.json"),JsonSerializer.Serialize(segments,JsonDefaults.Options),cancellationToken).ConfigureAwait(false);
    }
    public static async Task AtomicWriteAsync(string path,string text,CancellationToken cancellationToken)
    {
        var temporary=path+".tmp-"+Guid.NewGuid().ToString("N");
        try { await File.WriteAllTextAsync(temporary,text,Encoding.UTF8,cancellationToken).ConfigureAwait(false); File.Move(temporary,path,overwrite:true); }
        finally { if(File.Exists(temporary)) File.Delete(temporary); }
    }
}
