// lib/ui/widgets/registration_card.dart

import 'package:flutter/material.dart';
import '../../data/models/registration_model.dart';
import 'package:seniorhighscoolregistration/constants.dart';

class RegistrationCard extends StatelessWidget {
  final RegistrationModel registration;
  final VoidCallback onDelete;
  final VoidCallback? onTap;

  const RegistrationCard({
    super.key,
    required this.registration,
    required this.onDelete,
    this.onTap,
  });

  Color _strandColor(String strand) {
    switch (strand) {
      case 'ABM': return Colors.blue;
      case 'STEM': return Colors.green;
      case 'GAS': return Colors.orange;
      case 'HUMS': return Colors.purple;
      default: return Colors.grey;
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;
    final strandColor = _strandColor(registration.strand);

    return Card(
      margin: const EdgeInsets.symmetric(horizontal: 16, vertical: 5),
      elevation: 0,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(16),
        side: BorderSide(color: colorScheme.outline.withOpacity(0.2)),
      ),
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.all(14),
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.center,
            children: [
              // Strand badge
              Container(
                width: 46, height: 46,
                decoration: BoxDecoration(
                  color: strandColor.withOpacity(0.12),
                  borderRadius: BorderRadius.circular(12),
                ),
                alignment: Alignment.center,
                child: Text(
                  registration.strand,
                  style: TextStyle(color: strandColor, fontSize: 10, fontWeight: FontWeight.w700),
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(registration.studentName,
                        style: theme.textTheme.titleSmall?.copyWith(fontWeight: FontWeight.w600)),
                    const SizedBox(height: 2),
                    Row(
                      children: [
                        Icon(Icons.person_outline, size: 12, color: colorScheme.onSurfaceVariant),
                        const SizedBox(width: 3),
                        Flexible(
                          child: Text(registration.staffName,
                              style: theme.textTheme.bodySmall?.copyWith(
                                  color: colorScheme.onSurfaceVariant),
                              overflow: TextOverflow.ellipsis),
                        ),
                        const SizedBox(width: 8),
                        Icon(Icons.calendar_today_outlined, size: 12, color: colorScheme.onSurfaceVariant),
                        const SizedBox(width: 3),
                        Flexible(
                          child: Text(registration.date,
                              style: theme.textTheme.bodySmall?.copyWith(
                                  color: colorScheme.onSurfaceVariant),
                              overflow: TextOverflow.ellipsis),
                        ),
                      ],
                    ),
                    const SizedBox(height: 5),
                    Row(
                      children: [
                        Container(
                          padding: const EdgeInsets.symmetric(horizontal: 7, vertical: 2),
                          child: Text(
                            AppConstants.strandDescriptions[registration.strand] ?? registration.strand,
                            style: TextStyle(
                                color: strandColor,
                                fontSize: 10,
                                fontWeight: FontWeight.w500,
                            ),
                          ),
                        ),
                        if (registration.documentName != null) ...[
                          const SizedBox(width: 6),
                          Icon(Icons.attach_file, size: 12, color: colorScheme.primary),
                        ],
                      ],
                    ),
                  ],
                ),
              ),
              // Actions
              Column(
                children: [
                  if (onTap != null)
                    Icon(Icons.chevron_right, color: colorScheme.onSurfaceVariant.withOpacity(0.4), size: 18),
                  IconButton(
                    onPressed: () => _confirmDelete(context),
                    icon: const Icon(Icons.delete_outline),
                    color: colorScheme.error.withOpacity(0.6),
                    tooltip: 'Delete',
                    iconSize: 18,
                    visualDensity: VisualDensity.compact,
                    padding: EdgeInsets.zero,
                    constraints: const BoxConstraints(minWidth: 32, minHeight: 32),
                  ),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }

  Future<void> _confirmDelete(BuildContext context) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
        title: const Text('Delete Registration'),
        content: Text("Delete ${registration.studentName}'s registration?"),
        actions: [
          TextButton(onPressed: () => Navigator.pop(ctx, false), child: const Text('Cancel')),
          FilledButton(
            onPressed: () => Navigator.pop(ctx, true),
            style: FilledButton.styleFrom(backgroundColor: Theme.of(context).colorScheme.error),
            child: const Text('Delete'),
          ),
        ],
      ),
    );
    if (confirmed == true) onDelete();
  }
}
