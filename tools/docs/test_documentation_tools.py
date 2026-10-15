"""آزمون‌های خالص ابزار مستندات؛ بدون دسترسی SQL، شبکه یا secret."""

import unittest
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


if __name__ == '__main__':
    unittest.main()
