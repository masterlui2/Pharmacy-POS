// lib/ui/widgets/document_picker_tile.dart

import 'package:flutter/material.dart';

class DocumentPickerTile extends StatelessWidget {
  final String? fileName;
  final VoidCallback onPick;
  final VoidCallback onClear;

  const DocumentPickerTile({
    super.key,
    this.fileName,
    required this.onPick,
    required this.onClear,
  });

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;
    final hasFile = fileName != null && fileName!.isNotEmpty;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          'Document',
          style: theme.textTheme.labelLarge?.copyWith(
            color: colorScheme.onSurfaceVariant,
            fontWeight: FontWeight.w600,
          ),
        ),
        const SizedBox(height: 4),
        Text(
          'Government ID, Birth Certificate, or none',
          style: theme.textTheme.bodySmall?.copyWith(
            color: colorScheme.onSurfaceVariant.withOpacity(0.7),
          ),
        ),
        const SizedBox(height: 8),
        AnimatedContainer(
          duration: const Duration(milliseconds: 200),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(12),
            border: Border.all(
              color: hasFile
                  ? colorScheme.primary
                  : colorScheme.outline.withOpacity(0.5),
              width: hasFile ? 1.5 : 1,
            ),
            color: hasFile
                ? colorScheme.primaryContainer.withOpacity(0.2)
                : colorScheme.surfaceContainerHighest.withOpacity(0.3),
          ),
          child: hasFile ? _FileSelectedTile(
            fileName: fileName!,
            onClear: onClear,
            colorScheme: colorScheme,
            theme: theme,
          ) : _FilePickerButton(
            onPick: onPick,
            colorScheme: colorScheme,
            theme: theme,
          ),
        ),
      ],
    );
  }
}

class _FilePickerButton extends StatelessWidget {
  final VoidCallback onPick;
  final ColorScheme colorScheme;
  final ThemeData theme;

  const _FilePickerButton({
    required this.onPick,
    required this.colorScheme,
    required this.theme,
  });

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onPick,
      borderRadius: BorderRadius.circular(12),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Row(
          children: [
            Icon(
              Icons.upload_file_outlined,
              color: colorScheme.primary,
              size: 28,
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    'Upload document',
                    style: theme.textTheme.bodyMedium?.copyWith(
                      color: colorScheme.primary,
                      fontWeight: FontWeight.w500,
                    ),
                  ),
                  Text(
                    'PDF, JPG, or PNG',
                    style: theme.textTheme.bodySmall?.copyWith(
                      color: colorScheme.onSurfaceVariant.withOpacity(0.6),
                    ),
                  ),
                ],
              ),
            ),
            Icon(
              Icons.chevron_right,
              color: colorScheme.primary.withOpacity(0.6),
            ),
          ],
        ),
      ),
    );
  }
}

class _FileSelectedTile extends StatelessWidget {
  final String fileName;
  final VoidCallback onClear;
  final ColorScheme colorScheme;
  final ThemeData theme;

  const _FileSelectedTile({
    required this.fileName,
    required this.onClear,
    required this.colorScheme,
    required this.theme,
  });

  IconData _getFileIcon() {
    final ext = fileName.split('.').last.toLowerCase();
    if (ext == 'pdf') return Icons.picture_as_pdf_outlined;
    if (['jpg', 'jpeg', 'png'].contains(ext)) return Icons.image_outlined;
    return Icons.insert_drive_file_outlined;
  }

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.all(16),
      child: Row(
        children: [
          Container(
            padding: const EdgeInsets.all(8),
            decoration: BoxDecoration(
              color: colorScheme.primary.withOpacity(0.1),
              borderRadius: BorderRadius.circular(8),
            ),
            child: Icon(
              _getFileIcon(),
              color: colorScheme.primary,
              size: 24,
            ),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  fileName,
                  style: theme.textTheme.bodyMedium?.copyWith(
                    fontWeight: FontWeight.w500,
                    color: colorScheme.onSurface,
                  ),
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                ),
                Text(
                  'Document attached',
                  style: theme.textTheme.bodySmall?.copyWith(
                    color: colorScheme.primary,
                  ),
                ),
              ],
            ),
          ),
          IconButton(
            onPressed: onClear,
            icon: const Icon(Icons.close),
            color: colorScheme.error,
            tooltip: 'Remove document',
          ),
        ],
      ),
    );
  }
}