"""آزمون‌های خالص ابزار مستندات؛ بدون دسترسی SQL، شبکه یا secret."""

import unittest
import csv
from pathlib import Path
from tempfile import TemporaryDirectory
from unittest.mock import patch
import sync_repository_docs as sync


class DocumentationToolsTests(unittest.TestCase):
    def test_reference_is_linked_to_exact_contract(self):
        self.assertEqual('[VerifyOtpResult](#contract-VerifyOtpResult)', sync.schema_label({'$ref': '#/components/schemas/VerifyOtpResult'}))

    def test_array_preserves_item_contract(self):
        self.assertEqual('آرایه از [UserSessionResult](#contract-UserSessionResult)', sync.schema_label({'type': 'array', 'items': {'$ref': '#/components/schemas/UserSessionResult'}}))

    def test_guid_route_constraint_is_not_a_second_endpoint(self):
        self.assertEqual('/api/v1/files/{fileId}', sync.canonical('/api/v1/files/{fileId:guid}/'))

    def test_null_does_not_invent_a_contract(self):
        self.assertEqual('بدون schema منتشرشده', sync.schema_label({}))

    def test_table_cells_escape_sql_or_markdown_pipes(self):
        self.assertEqual('a&#124;b c', sync.cell('a|b\nc'))

    def test_enum_retains_literal_values(self):
        self.assertIn('Minor, Adult, Unknown', sync.schema_label({'type': 'string', 'enum': ['Minor', 'Adult', 'Unknown']}))

    def test_composed_schema_retains_every_branch(self):
        value = sync.schema_label({'oneOf': [{'$ref': '#/components/schemas/First'}, {'$ref': '#/components/schemas/Second'}]})
        self.assertIn('#contract-First', value)
        self.assertIn('#contract-Second', value)

    def test_review_notice_is_idempotent_and_keeps_original_decision(self):
        source = '# عنوان\n\nتصمیم مصوب نباید تغییر کند.\n'
        notice = '\n<!-- DOC-SYNC:BEGIN -->\nبرچسب بازبینی\n<!-- DOC-SYNC:END -->\n'
        first = sync.review_notice(source, notice)
        self.assertEqual(first, sync.review_notice(first, notice))
        self.assertEqual(1, first.count('DOC-SYNC:BEGIN'))
        self.assertIn('تصمیم مصوب نباید تغییر کند.', first)


    def test_enum_notice_closes_the_resolved_integer_string_gap(self):
        api = {'components': {'schemas': {'MessageKind': {'type': 'string'}}}}
        inventory = {'Enums': [{'Name': 'MessageKind', 'Values': ['Text', 'Poll']}]}
        self.assertEqual([], sync.enum_contract_mismatches(api, inventory))
        self.assertIn('CLOSED_IN_CODE_AND_LOCAL_CONTRACT_TESTS', sync.enum_contract_notice(api, inventory))

    def test_enum_notice_keeps_a_remaining_transport_mismatch_open(self):
        api = {'components': {'schemas': {'MessageKind': {'type': 'integer'}}}}
        inventory = {'Enums': [{'Name': 'MessageKind', 'Values': ['Text', 'Poll']}]}
        self.assertEqual(['MessageKind'], sync.enum_contract_mismatches(api, inventory))
        self.assertNotIn('CLOSED_IN_CODE_AND_LOCAL_CONTRACT_TESTS', sync.enum_contract_notice(api, inventory))

    def test_inventory_preserves_historical_evidence_by_default(self):
        with TemporaryDirectory() as directory:
            root = Path(directory)
            historical = root / 'docs/architecture/decision.md'
            runbook = root / 'docs/operations/runbook.md'
            audit = root / 'docs/operations/PRODUCTION_HARDENING_AUDIT.md'
            historical.parent.mkdir(parents=True)
            runbook.parent.mkdir(parents=True)
            original = '# تصمیم مصوب\r\n\r\nتست تاریخی: ۷ موفق.\r\n'.encode('utf-8')
            historical.write_bytes(original)
            runbook.write_bytes(original)
            audit.write_bytes(original)
            tracked = 'docs/architecture/decision.md\ndocs/operations/runbook.md\ndocs/operations/PRODUCTION_HARDENING_AUDIT.md\n'
            with patch.object(sync, 'ROOT', root), patch.object(sync.subprocess, 'check_output', return_value=tracked):
                sync.document_inventory('abc1234', '2026-10-23')
            self.assertEqual(original, historical.read_bytes())
            self.assertEqual(original, runbook.read_bytes())
            self.assertEqual(original, audit.read_bytes())
            with (root / 'docs/documentation-inventory.csv').open(encoding='utf-8-sig', newline='') as stream:
                rows = {row['Path']: row for row in csv.DictReader(stream)}
            self.assertEqual('راهنمای جاری', rows['docs/operations/runbook.md']['DocumentKind'])
            self.assertEqual('ممیزی تاریخی عملیات', rows['docs/operations/PRODUCTION_HARDENING_AUDIT.md']['DocumentKind'])

    def test_explicit_notice_refresh_still_preserves_decision_and_current_runbooks(self):
        with TemporaryDirectory() as directory:
            root = Path(directory)
            historical = root / 'docs/architecture/decision.md'
            runbook = root / 'docs/operations/runbook.md'
            historical.parent.mkdir(parents=True)
            runbook.parent.mkdir(parents=True)
            original = '# تصمیم\n\nمبنای پذیرش همان اجرای تاریخی است.\n'
            historical.write_text(original, encoding='utf-8')
            runbook.write_text(original, encoding='utf-8')
            tracked = 'docs/architecture/decision.md\ndocs/operations/runbook.md\n'
            with patch.object(sync, 'ROOT', root), patch.object(sync.subprocess, 'check_output', return_value=tracked):
                sync.document_inventory('abc1234', '2026-10-23', refresh_historical_notices=True)
            self.assertIn('DOC-SYNC:BEGIN', historical.read_text(encoding='utf-8'))
            self.assertIn('مبنای پذیرش همان اجرای تاریخی است.', historical.read_text(encoding='utf-8'))
            self.assertEqual(original, runbook.read_text(encoding='utf-8'))


if __name__ == '__main__':
    unittest.main()
