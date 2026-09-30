#!/usr/bin/env bash
# Reports the memory used by each running container of a Compose project and fails when the
# total exceeds a budget (MiB). Writes a Markdown table to the GitHub job summary when available.
#
#   perf/measure-memory.sh 350            # budget for the idle stack
#   perf/measure-memory.sh 450 cadence    # explicit project name
#
# Containers labelled cadence.test-only=true (Mailpit in end-to-end runs) are not part of a
# real deployment, so they are left out.
set -euo pipefail

budget_mib="${1:?Usage: measure-memory.sh <budget-MiB> [compose-project]}"
project="${2:-cadence}"

mapfile -t containers < <(
  docker ps --filter "label=com.docker.compose.project=${project}"     --format '{{.ID}} {{.Label "cadence.test-only"}}' | awk '$2 != "true" { print $1 }'
)
if ((${#containers[@]} == 0)); then
  echo "No running containers found for Compose project '${project}'." >&2
  exit 1
fi

# docker stats prints usage like "43.76MiB / 320MiB"; normalise the first value to MiB.
report="$(docker stats --no-stream --format '{{.Name}}\t{{.MemUsage}}' "${containers[@]}" |
  awk -F'\t' '
    function to_mib(value,   number, unit) {
      number = value + 0
      unit = value
      gsub(/[0-9.]/, "", unit)
      if (unit == "GiB") return number * 1024
      if (unit == "MiB") return number
      if (unit == "KiB") return number / 1024
      if (unit == "B")   return number / 1048576
      return number
    }
    {
      split($2, parts, " / ")
      used = to_mib(parts[1])
      total += used
      printf "| %s | %.1f MiB | %s |\n", $1, used, parts[2]
    }
    END { printf "TOTAL %.1f\n", total }')"

total="$(grep '^TOTAL' <<< "$report" | cut -d' ' -f2)"
table="$(printf '| Container | Memory | Limit |\n|---|---|---|\n%s\n| **Total** | **%s MiB** | budget %s MiB |\n' \
  "$(grep -v '^TOTAL' <<< "$report")" "$total" "$budget_mib")"

echo "$table"
if [[ -n "${GITHUB_STEP_SUMMARY:-}" ]]; then
  printf '### Memory (%s)\n\n%s\n' "${MEMORY_LABEL:-idle}" "$table" >> "$GITHUB_STEP_SUMMARY"
fi

if awk -v total="$total" -v budget="$budget_mib" 'BEGIN { exit !(total > budget) }'; then
  echo "Memory budget exceeded: ${total} MiB used, budget ${budget_mib} MiB." >&2
  exit 1
fi
echo "Memory within budget: ${total} MiB of ${budget_mib} MiB."
