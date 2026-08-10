// Zero-dependency click-to-sort table headers. Apply by adding class="sortable-table" to a <table>
// and data-sort="text" or data-sort="numeric" to any <th> that should be sortable. Optionally set
// data-sort-value on a <td> to sort by something other than its displayed text (e.g. a raw
// timestamp or numeric count behind a formatted display string).
(function () {
    function getCellValue(row, index) {
        var cell = row.children[index];
        if (!cell) {
            return '';
        }
        if (cell.dataset.sortValue !== undefined) {
            return cell.dataset.sortValue;
        }
        return cell.textContent.trim();
    }

    function compareValues(a, b, numeric) {
        if (numeric) {
            var numA = parseFloat(a);
            var numB = parseFloat(b);
            if (isNaN(numA)) {
                numA = -Infinity;
            }
            if (isNaN(numB)) {
                numB = -Infinity;
            }
            return numA - numB;
        }
        return a.localeCompare(b, undefined, { sensitivity: 'base' });
    }

    function initSortableTable(table) {
        var tbody = table.tBodies[0];
        if (!tbody) {
            return;
        }

        var headers = table.querySelectorAll('thead th[data-sort]');
        headers.forEach(function (th) {
            th.addEventListener('click', function () {
                var index = Array.prototype.indexOf.call(th.parentElement.children, th);
                var numeric = th.getAttribute('data-sort') === 'numeric';
                var newDirection = th.getAttribute('data-sort-dir') === 'asc' ? 'desc' : 'asc';

                headers.forEach(function (header) {
                    header.removeAttribute('data-sort-dir');
                    header.classList.remove('sorted-asc', 'sorted-desc');
                });
                th.setAttribute('data-sort-dir', newDirection);
                th.classList.add(newDirection === 'asc' ? 'sorted-asc' : 'sorted-desc');

                var rows = Array.prototype.slice.call(tbody.querySelectorAll('tr'));
                rows.sort(function (rowA, rowB) {
                    var result = compareValues(getCellValue(rowA, index), getCellValue(rowB, index), numeric);
                    return newDirection === 'asc' ? result : -result;
                });

                rows.forEach(function (row) {
                    tbody.appendChild(row);
                });
            });
        });
    }

    document.addEventListener('DOMContentLoaded', function () {
        document.querySelectorAll('table.sortable-table').forEach(initSortableTable);
    });
})();
