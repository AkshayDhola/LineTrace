namespace LineTrace.Report.Helpers;

internal static class HtmlTemplate
{
    public const string Head = """
        <!doctype html>
        <html lang="en">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <title>
        """;

    public const string Side = $$"""
         · LineTrace</title>
        <style>
        {{Css}}
        </style>
        </head>
        <body>
        <aside class="side">
          <div class="brand">LineTrace</div>
          <div class="report">
        """;

    public const string Run = """</div><dl class="run">""";

    public const string Nav = """
        </dl>
          <nav>
            <div class="label">Cases</div>
        """;

    public const string Page = """
          </nav>
        </aside>
        <main>
          <header class="page-head">
            <h1>
        """;

    public const string Notes = """
        </h1>
            <ul class="notes">
              <li>Values are averaged over all iterations of the traced method; the first (cold) run is included.</li>
              <li>Allocations are exact. Times have the tracer's own overhead removed but come from patched code (try/finally and hook calls in every method), so compare shares, not absolute numbers.</li>
              <li>Self excludes calls into other methods; a line's Time and Alloc include the calls made on it. For recursive methods only Self is exact, and GC pauses land on the lines that allocate.</li>
              <li>Hook-cost subtraction leaves a few ns of noise per call; times inside that noise show as 0.</li>
            </ul>
          </header>
        """;

    public const string End = $$"""
        </main>
        <script>
        {{Script}}
        </script>
        </body>
        </html>
        """;

    public const string Tools = """
        <span class="tools"><button type="button" data-open="true">Expand all</button><button type="button" data-open="false">Collapse all</button></span></summary>
        """;

    public const string TreeHead = """
        <div class="tree"><div class="row head"><span>Method</span><span>Share</span><span>Calls</span><span>Time</span><span>Self</span><span>Alloc</span><span>Self alloc</span></div>
        """;

    public const string LineHead = """
        </span></summary><table><thead><tr><th class="num">Line</th><th class="num">Hits</th><th class="num">Time</th><th class="num">Self</th><th class="num">Alloc</th><th class="num">Self alloc</th><th class="src">Source</th></tr></thead><tbody>
        """;

    private const string Css = """
        :root {
          --bg: #f7f7f5;
          --surface: #ffffff;
          --head: #fafaf9;
          --hover: #f2f2f0;
          --border: #e3e3e0;
          --ink: #0b0b0b;
          --ink2: #52514e;
          --time: #2a78d6;
          --alloc: #eb6834;
          --sans: system-ui, -apple-system, "Segoe UI", sans-serif;
          --mono: ui-monospace, SFMono-Regular, Menlo, Consolas, "DejaVu Sans Mono", "Liberation Mono", monospace;
          --pad: 24px;    /* the one edge: sidebar, page, card heads, first and last columns */
          --side: 248px;
          --step: 20px;   /* tree indent per level */
          --share: 124px;
          --num: 96px;    /* every number column */
          color-scheme: light;
          scrollbar-width: thin;
          scrollbar-color: #c8c7c1 transparent;
        }

        * { box-sizing: border-box; }

        body {
          margin: 0;
          display: grid;
          grid-template-columns: var(--side) minmax(0, 1fr);
          min-height: 100vh;
          background: var(--bg);
          color: var(--ink);
          font: 16px/1.5 var(--sans);
        }

        summary { list-style: none; cursor: pointer; }
        summary::-webkit-details-marker { display: none; }

        button {
          padding: 3px 12px;
          border: 1px solid var(--border);
          background: var(--surface);
          color: var(--ink2);
          font: 14px/1.4 var(--sans);
          cursor: pointer;
        }
        button:hover { background: var(--hover); color: var(--ink); }

        /* Sidebar */
        .side {
          position: sticky;
          top: 0;
          height: 100vh;
          overflow-y: auto;
          padding: var(--pad);
          background: var(--surface);
          border-right: 1px solid var(--border);
        }
        .brand, .label {
          font-size: 13px;
          font-weight: 600;
          letter-spacing: .08em;
          text-transform: uppercase;
          color: var(--ink2);
        }
        .report { margin: 4px 0 var(--pad); font-size: 20px; font-weight: 700; line-height: 1.3; overflow-wrap: break-word; }
        .run { display: grid; grid-template-columns: auto 1fr; gap: 6px 12px; margin: 0 0 var(--pad); font-size: 14px; }
        .run dt { color: var(--ink2); }
        .run dd { margin: 0; overflow-wrap: anywhere; }
        .label { margin-bottom: 4px; }
        nav a { display: block; padding: 3px 0; color: var(--ink); text-decoration: none; }
        nav a:hover { text-decoration: underline; }
        nav .case { margin-top: 12px; font: 600 14px/1.4 var(--mono); overflow-wrap: break-word; }
        nav .section { padding-left: var(--step); font-size: 14px; color: var(--ink2); }

        /* Page */
        main { min-width: 0; padding: var(--pad) var(--pad) 80px; }
        .page-head h1 { margin: 0 0 12px; font-size: 31px; line-height: 1.2; font-weight: 700; overflow-wrap: break-word; }
        .notes { margin: 0; padding-left: 20px; color: var(--ink2); font-size: 15px; }
        .notes li { margin: 2px 0; }

        .case { margin-top: 40px; scroll-margin-top: var(--pad); }
        .case-head h2 { margin: 0 0 12px; font: 600 25px/1.25 var(--mono); overflow-wrap: break-word; }
        .tiles { display: flex; flex-wrap: wrap; gap: 12px; }
        .tile { min-width: 200px; padding: 10px var(--pad); background: var(--surface); border: 1px solid var(--border); }
        .tile small { display: block; font-size: 14px; color: var(--ink2); }
        .tile b { font-size: 20px; font-variant-numeric: tabular-nums; }

        .card { margin-top: 16px; background: var(--surface); border: 1px solid var(--border); scroll-margin-top: var(--pad); }
        .card-head {
          display: flex;
          flex-wrap: wrap;
          align-items: center;
          gap: 4px 16px;
          padding: 12px var(--pad);
          border-bottom: 1px solid var(--border);
        }
        .card:not([open]) > .card-head { border-bottom: 0; }
        .card-head .title { font-size: 20px; font-weight: 600; }
        .hint { font-size: 14px; color: var(--ink2); }
        .tools { display: flex; gap: 8px; margin-left: auto; }

        /* Call tree: indentation is the only structure marker */
        .tree, table { font-size: 15px; line-height: 1.45; font-variant-numeric: tabular-nums; }
        .row {
          display: grid;
          grid-template-columns: minmax(240px, 1fr) var(--share) repeat(5, var(--num));
          column-gap: 8px;
          align-items: center;
          padding: 5px var(--pad);
          border-bottom: 1px solid var(--border);
        }
        .row > span:not(.name) { text-align: right; }
        .row.head {
          position: sticky;
          top: 0;
          z-index: 1;
          background: var(--head);
          color: var(--ink2);
          font-size: 14px;
          font-weight: 600;
        }
        .row.head > span:first-child { text-align: left; }
        .tree summary.row:hover, .tree div.row:not(.head):hover { background: var(--hover); }
        .name { padding-left: calc(var(--d) * var(--step)); font: 14px/1.45 var(--mono); overflow-wrap: anywhere; }
        .at { color: var(--ink2); }
        .tag { margin-left: 8px; padding: 0 6px; border: 1px solid var(--border); color: var(--ink2); font: 13px var(--sans); }
        .share { display: flex; align-items: center; justify-content: flex-end; gap: 8px; }
        .track { flex: 0 0 56px; height: 6px; background: var(--border); }
        .bar { display: block; height: 100%; width: var(--w); background: var(--time); }

        /* Line report */
        .method { border-bottom: 1px solid var(--border); }
        .method:last-child { border-bottom: 0; }
        .method > summary {
          display: flex;
          flex-wrap: wrap;
          align-items: baseline;
          gap: 4px 16px;
          padding: 10px var(--pad);
        }
        .method > summary:hover { background: var(--hover); }
        .method > summary b { font: 600 15px/1.4 var(--mono); overflow-wrap: anywhere; }
        .method[open] > summary { background: var(--head); border-bottom: 1px solid var(--border); }
        table { width: 100%; border-collapse: collapse; table-layout: fixed; } /* same columns in every method block */
        th, td { padding: 3px 0 3px 8px; border-bottom: 1px solid var(--border); white-space: nowrap; vertical-align: top; }
        th { position: sticky; top: 0; z-index: 1; background: var(--head); color: var(--ink2); font-size: 14px; font-weight: 600; }
        tr > :first-child { padding-left: var(--pad); }
        tr > :last-child { padding-right: var(--pad); }
        tbody tr:hover { background: var(--hover); }
        .num { width: var(--num); text-align: right; }
        th.num:first-child { width: calc(56px + var(--pad)); } /* line numbers */
        .src { padding-left: var(--pad); text-align: left; white-space: normal; }
        code { font: 14px/1.45 var(--mono); white-space: pre-wrap; overflow-wrap: anywhere; }
        .heat.time { background: color-mix(in srgb, var(--time) calc(var(--h) * 45%), transparent); }
        .heat.alloc { background: color-mix(in srgb, var(--alloc) calc(var(--h) * 45%), transparent); }

        @media (max-width: 1440px) {
          :root { --side: 220px; --step: 14px; --num: 84px; --share: 96px; }
          .track { flex-basis: 36px; }
        }
        @media (max-width: 1100px) {
          body { display: block; }
          .side { position: static; height: auto; border-right: 0; border-bottom: 1px solid var(--border); }
        }
        """;

    private const string Script = """
        document.addEventListener('click', event => {
          const button = event.target.closest('button[data-open]');
          if (!button) return;
          event.preventDefault(); // the buttons sit in the card's <summary>; don't toggle the card itself
          const open = button.dataset.open === 'true';
          const card = button.closest('details.card');
          card.open = true;
          card.querySelectorAll('details').forEach(d => { if (d !== card) d.open = open; });
        });
        """;
}
